# 构建、发布和密钥管理

## 工作流

`ci.yml`：Rustfmt、Clippy、Rust 真实业务测试、Vitest、TypeScript/Vite 构建、Windows 原生 WebView 验收，及 Windows x64 / macOS Intel / Apple Silicon 实际安装包构建。

`release.yml`：SemVer tag 与 package/Cargo/Tauri 版本一致，三平台构建 → Tauri 签名 → 同算法签名/版本校验、篡改反例 → 全部资产清单及 SHA256 → GitHub Release。缺一架构就不发布。`latest.json` 仅包含可作为正式更新发布的平台。

`pages.yml`：解析实际存在的 Release 资产，运行桌面/移动、深浅主题、200%文字和 WCAG AA 验收，再部署 GitHub Pages。

Actions 固定到已验证的 commit SHA，默认最小权限。只有发布汇总 job 有 `contents: write`，Pages 有 `pages: write` / `id-token: write`。

## GitHub Secrets

| Secret | 用途 |
| --- | --- |
| `TAURI_SIGNING_PRIVATE_KEY` | Tauri 官方 signer 生成的 updater 私钥；所有更新包签名 |
| `TAURI_SIGNING_PRIVATE_KEY_PASSWORD` | 私钥密码，空密码可不设置 |
| `APPLE_CERTIFICATE` | base64 编码的 Developer ID Application `.p12` |
| `APPLE_CERTIFICATE_PASSWORD` | `.p12` 密码 |
| `APPLE_SIGNING_IDENTITY` | Developer ID Application 身份 |
| `APPLE_ID` | 公证使用的 Apple ID |
| `APPLE_PASSWORD` | Apple 应用专用密码 |
| `APPLE_TEAM_ID` | 开发者 Team ID |

私钥、证书、密码只通过 GitHub Secrets 安全配置，不放入源码、聊天或构建日志。Apple 凭据须完整配置；不完整组会失败，避免误认部分签名为正式构建。若全部缺失，产出明确标识的未公证预览包，`latest.json` 排除 macOS。

macOS 完整组通过官方 Tauri bundler 导入证书、签名和公证；工作流进一步运行 `codesign --verify --deep --strict`、`stapler validate` 与 `spctl --assess`。只有全部通过才记录 `notarized: true`。当前没有 Apple 组，macOS 正式分发为客观阻塞。

## 更新验证

公钥在 `src-tauri/tauri.conf.json`，`requireSignedVersion: true`。`crates/release-tools` 使用官方插件相同的 `minisign-verify` 库，先验证包及 trusted comment 的全局签名，再比较签名中的版本与声明版本。自动验证改变一个字节和伪造版本都被拒绝。

每次发布请下载最终 GitHub 资产再次验证 SHA256、`.sig` 及 `latest.json` URL。安装/在线升级属于独立验收，不用“构建成功”代替。

## 仓库迁移

旧仓库重命名后仓库 ID、refs、Release ID、资产 ID/名称/大小保持一致；本地额外保留完整 bundle、mirror、Release 资产及原工作区未提交改动备份。新仓库单独创建，没有覆盖旧历史。新开发在 `codex/v3-rebuild`，验收通过后合并 main 再创建版本 tag。
# Windows 真实更新验收

本次使用未发布的 `3.0.0-qa.0` 引导程序，连接正式 GitHub Releases 的 `latest.json` 与实际签名 NSIS，验证稍后、跳过、下载、安装、重启及合成归档保留。测试不会启动自定义更新服务器或替换生产公钥。

在 ignored `.cache/updater-bootstrap/version.json` 写入 `{"version":"3.0.0-qa.0"}`，通过 `node scripts/build-desktop.mjs --no-bundle --config .cache/updater-bootstrap/version.json` 构建并复制可执行文件。发布后下载实际 NSIS，运行 `node scripts/windows-upgrade-check.mjs <实际安装包路径> <引导程序路径>`。测试要求默认数据目录没有任何现有发票；已有数据时拒绝运行。测试数据明确为合成内容，完成后将其完整移入本地备份目录，并卸载测试安装。

