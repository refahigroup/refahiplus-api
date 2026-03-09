# تنظیم Environment Variables برای Production

> 🎯 **خلاصه:** Deploy.ps1 خودکار web.config را حفظ می‌کند. فقط یک بار تنظیم کنید و دیگر نگران نباشید!

## مشکل
در پروداکشن (IIS)، environment variableها درست لود نمیشدند چون:
1. IIS به صورت پیش‌فرض environment variableهای system رو نمیخونه
2. نیاز به تنظیم در `web.config` داریم
3. کد configuration باید placeholderها رو replace کنه

## ✅ راه حل نهایی: حفظ خودکار web.config در Deploy

**مشکل:** با هر publish، `web.config` جدید جایگزین می‌شد و تنظیمات production از دست می‌رفت.

**راهکار:** اسکریپت `Deploy.ps1` به گونه‌ای اصلاح شد که:

1. **قبل از deploy**: `web.config` موجود روی سرور را backup می‌گیرد
2. **بعد از extract**: فایل backup شده را restore می‌کند
3. **نتیجه**: تنظیمات production همیشه حفظ می‌شوند! 🎯

```powershell
# این کار خودکار در Deploy.ps1 انجام می‌شود:
# 1. Backup web.config
$webConfigBackup = Join-Path $env:TEMP "web.config.backup-$(Get-Date -Format 'yyyyMMddHHmmss')"
Copy-Item -Path $webConfigPath -Destination $webConfigBackup -Force

# 2. Deploy new version
Expand-Archive -Path $remoteZip -DestinationPath $sitePath -Force

# 3. Restore production web.config
Copy-Item -Path $webConfigBackup -Destination $webConfigPath -Force
```

### ✨ مزایا:
- ✅ یک بار تنظیم کنید، برای همیشه حفظ می‌شود
- ✅ هیچ کار اضافی در هر deploy نیاز نیست
- ✅ از Visual Studio هم کار می‌کند
- ✅ ایمن - فایل اصلی backup می‌شود قبل از هر تغییر

## راه حل پیاده شده

### 1. فایل web.config
فایل `web.config` با environment variables ایجاد شد:
```xml
<environmentVariables>
  <environmentVariable name="REFAHIPLUS_DB_USER" value="CHANGE_ME" />
  <environmentVariable name="REFAHIPLUS_DB_PASSWORD" value="CHANGE_ME" />
  <environmentVariable name="REFAHIPLUS_JWT_KEY" value="CHANGE_ME" />
  <environmentVariable name="REFAHIPLUS_SNAPPTRIP_API_KEY" value="CHANGE_ME" />
</environmentVariables>
```

### 2. کد اصلاح شد
- ✅ JWT Key: اصلاح شد در `Refahi.Modules.Identity.Api/DI.cs`
- ✅ SnappTrip ApiKey: اصلاح شد با PostConfigure در `SnappTrip/DI.cs`
- ✅ ConnectionString: قبلا اصلاح شده بود در `ConfigurationExtensions.cs`

### 3. نحوه تنظیم مقادیر واقعی در Production (فقط یک بار!)

> ⚠️ **توجه:** این کار را فقط یک بار انجام دهید. Deploy.ps1 خودکار web.config را حفظ می‌کند.

#### گزینه 1: ویرایش مستقیم web.config روی سرور (توصیه می‌شود)

**مرحله 1:** اولین deploy را انجام دهید (با مقادیر CHANGE_ME)

```powershell
cd C:\Workspace\Refahi\repo\Refahi_WorkSpace\deployment\web-api
.\Deploy.ps1
```

**مرحله 2:** به سرور وصل شوید و web.config را ویرایش کنید

```powershell
# اتصال به سرور
$cred = Import-Clixml "$env:USERPROFILE\.refahi\server-credential.xml"
$session = New-PSSession -ComputerName 95.156.253.171 -Credential $cred

# ویرایش web.config
Invoke-Command -Session $session -ScriptBlock {
    $configPath = "C:\Workspace\RUN\RefahiPlus-WebApi\web.config"
    
    # بک‌آپ امنیتی
    Copy-Item $configPath "$configPath.backup-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    
    # خواندن محتوا
    $xml = [xml](Get-Content $configPath)
    
    # تنظیم مقادیر واقعی
    $envVars = $xml.configuration.location.'system.webServer'.aspNetCore.environmentVariables
    
    foreach ($var in $envVars.environmentVariable) {
        switch ($var.name) {
            "REFAHIPLUS_DB_USER" { 
                $var.value = "your_actual_db_user" 
            }
            "REFAHIPLUS_DB_PASSWORD" { 
                $var.value = "your_actual_db_password" 
            }
            "REFAHIPLUS_JWT_KEY" { 
                # حداقل 32 کاراکتر - مثال:
                $var.value = "MyVerySecureJwtKeyWithAtLeast32Characters!"
            }
            "REFAHIPLUS_SNAPPTRIP_API_KEY" { 
                $var.value = "your_actual_snapptrip_api_key" 
            }
        }
    }
    
    # ذخیره
    $xml.Save($configPath)
    
    Write-Host "web.config updated successfully!" -ForegroundColor Green
    
    # Restart App Pool
    Import-Module WebAdministration
    Restart-WebAppPool -Name "RefahiPlus-WebApi"
    Write-Host "App Pool restarted" -ForegroundColor Green
}

Remove-PSSession $session
```

**مرحله 3:** تست کنید

```powershell
curl http://95.156.253.171:8080/health
# یا
curl http://api.refahiplus.ir/health
```

**تمام!** از این به بعد، هر بار که deploy کنید، این تنظیمات خودکار حفظ می‌شوند. 🎉

---

#### گزینه 2: ویرایش دستی فایل web.config روی سرور
در محیط development، یک فایل `web.config.production` با مقادیر واقعی بسازید (و آن را به git اضافه نکنید!):

```powershell
# اضافه کردن به .gitignore
Add-Content .gitignore "`nweb.config.production"
```

سپس در script deploy، این فایل را جایگزین کنید.

#### گزینه 3: استفاده از Application Pool Environment Variables
می‌توانید environment variableها را مستقیما در Application Pool تنظیم کنید:

```powershell
Invoke-Command -ComputerName 95.156.253.171 -Credential $cred -ScriptBlock {
    Import-Module WebAdministration
    
    $appPoolName = "RefahiPlus-WebApi"
    
    # راه 1: استفاده از appcmd
    & $env:SystemRoot\system32\inetsrv\appcmd.exe set config -section:system.applicationHost/applicationPools /+"[name='$appPoolName'].environmentVariables.[name='REFAHIPLUS_DB_USER',value='your_db_user']"
    
    # Restart
    Restart-WebAppPool -Name $appPoolName
}
```

## تست کردن

بعد از تنظیم، تست کنید:
```powershell
# تست health endpoint
curl http://refahiplus.com/api/health

# بررسی logs در صورت خطا
Invoke-Command -ComputerName 95.156.253.171 -Credential $cred -ScriptBlock {
    Get-Content "C:\Workspace\RUN\RefahiPlus-WebApi\logs\stdout_*.log" -Tail 50
}
```

## نکات امنیتی

1. ⚠️ هرگز مقادیر واقعی environment variableها رو به git اضافه نکنید
2. ✅ از Azure Key Vault یا Windows Credential Manager برای نگهداری مقادیر حساس استفاده کنید
3. ✅ دسترسی به فایل web.config رو محدود کنید (فقط IIS_IUSRS)
4. ✅ JWT Key باید حداقل 32 کاراکتر باشه

## Troubleshooting

### مشکل: هنوز placeholder {REFAHIPLUS_*} در خطاها دیده می‌شود
**راه حل:** بررسی کنید که:
1. web.config در پوشه publish موجود باشه
2. مقادیر در web.config صحیح ست شده باشند (نه CHANGE_ME)
3. App Pool restart شده باشه

### مشکل: Environment variable null برمیگرده
**راه حل:**
1. بررسی syntax در web.config (باید درست بسته بشه)
2. مطمئن شوید که نام environment variable دقیقا مطابق باشد (case-sensitive نیست ولی دقت کنید)
3. Log stdout رو بررسی کنید برای error messages

### مشکل: بعد از deploy تغییرات apply نمیشه
**راه حل:**
```powershell
# Complete reset
Invoke-Command -ComputerName 95.156.253.171 -Credential $cred -ScriptBlock {
    Import-Module WebAdministration
    Stop-WebAppPool -Name "RefahiPlus-WebApi"
    Start-Sleep -Seconds 5
    Start-WebAppPool -Name "RefahiPlus-WebApi"
}
```
