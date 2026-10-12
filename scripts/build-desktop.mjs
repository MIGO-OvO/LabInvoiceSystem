import {spawn} from 'node:child_process';
import path from 'node:path';
const env={...process.env};
const apple=['APPLE_CERTIFICATE','APPLE_CERTIFICATE_PASSWORD','APPLE_SIGNING_IDENTITY','APPLE_ID','APPLE_PASSWORD','APPLE_TEAM_ID'];
for(const name of [...apple,'TAURI_SIGNING_PRIVATE_KEY_PASSWORD'])if(!env[name])delete env[name];
if(process.platform==='darwin'&&apple.some(name=>env[name])&&!apple.every(name=>env[name]))throw new Error('Apple signing/notarization credentials are incomplete. Configure the full secret group before building a signed distribution.');
if(process.platform==='darwin'&&!apple.some(name=>env[name])){env.APPLE_SIGNING_IDENTITY='-';console.log('Apple distribution credentials absent: ad-hoc signing for preview bundle integrity only; no notarization or production macOS updates.');}
if(!env.TAURI_SIGNING_PRIVATE_KEY)throw new Error('TAURI_SIGNING_PRIVATE_KEY is required to create verifiable updater artifacts.');
const child=spawn(process.execPath,[path.resolve('node_modules/@tauri-apps/cli/tauri.js'),'build',...process.argv.slice(2)],{env,stdio:'inherit',windowsHide:true});
child.on('error',error=>{console.error(error.message);process.exitCode=1;});child.on('exit',code=>process.exitCode=code??1);
