@echo off
echo starting....
cd LabInvoiceSystem
dotnet run
if %errorlevel% neq 0 (
    echo starting failed, please check error information.
    pause
)
