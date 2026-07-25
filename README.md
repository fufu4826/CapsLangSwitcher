# CapsLangSwitcher

Use `Caps Lock` as a Windows keyboard language switch key.

CapsLangSwitcher is a tiny Windows tray app that remaps `Caps Lock` to switch to the next installed keyboard input language. It is useful for Thai/English typing and for anyone who wants a single dedicated language-switch key.

## Features

- Press `Caps Lock` to change keyboard language
- Prevents normal Caps Lock toggling
- Runs quietly in the system tray
- Starts automatically with Windows after install
- Right-click tray menu to enable/disable, open Windows language settings, or exit
- No administrator permission required
- No internet connection required

## Download

Go to the latest GitHub Release and download `CapsLangSwitcher.zip`.

After extracting:

1. Double-click `Install.cmd`
2. Press `Caps Lock` in any text box to switch language
3. Use the tray icon in the bottom-right taskbar area to enable/disable or exit

To remove it, double-click `Uninstall.cmd`.

## Thai

โปรแกรมนี้ทำให้ปุ่ม `Caps Lock` กลายเป็นปุ่มเปลี่ยนภาษาใน Windows เหมาะสำหรับคนที่พิมพ์ไทย/อังกฤษบ่อย ๆ

วิธีใช้:

1. ดาวน์โหลด `CapsLangSwitcher.zip` จากหน้า Releases
2. แตกไฟล์ zip
3. ดับเบิลคลิก `Install.cmd`
4. กด `Caps Lock` เพื่อเปลี่ยนภาษา
5. ถ้าต้องการถอนการติดตั้ง ให้ดับเบิลคลิก `Uninstall.cmd`

หลังติดตั้ง โปรแกรมจะเปิดเองทุกครั้งที่เข้า Windows และจะมีไอคอนอยู่ใน system tray มุมขวาล่าง

## Requirements

- Windows 10 or Windows 11
- At least two keyboard input languages installed, for example English and Thai

## Build From Source

This project intentionally avoids heavy dependencies. It builds with the C# compiler available through Windows PowerShell/.NET Framework on most Windows installations.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\BuildExe.ps1
```

The output file is `CapsLangSwitcher.exe`.

## How It Works

CapsLangSwitcher installs a low-level keyboard hook for `Caps Lock`. When the key is pressed, it suppresses the normal Caps Lock behavior and asks Windows to switch the foreground window to the next preloaded keyboard layout.

## GitHub Topics

Recommended repository topics:

`windows`, `keyboard`, `keyboard-layout`, `capslock`, `language-switcher`, `thai-keyboard`, `tray-app`, `csharp`

## License

MIT
