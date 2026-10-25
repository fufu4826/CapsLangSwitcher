# CapsLangSwitcher

<p align="center"><img src="assets/CapsLangSwitcher.png" width="160" alt="CapsLangSwitcher icon"></p>

โปรแกรมเล็ก ๆ สำหรับ Windows ที่เปลี่ยนปุ่ม `Caps Lock` ให้เป็นปุ่มสลับภาษา

## วิธีใช้

1. ดาวน์โหลดไฟล์จาก repo นี้
2. ดับเบิลคลิก `CapsLangSwitcher.exe` แล้วกด Yes เมื่อ Windows ขอสิทธิ์ Administrator
3. กดแล้วปล่อย `Caps Lock` เพื่อสลับภาษา

กด `Caps Lock` ค้างแล้วกด `Spacebar` เพื่อเปิด/ปิด Caps Lock จริง โดยคอมโบนี้จะไม่สลับภาษาและไม่พิมพ์ช่องว่าง

เมื่อสลับภาษา โปรแกรมจะแสดงภาษาใหม่บนหน้าจอชั่วครู่โดยไม่แย่ง focus และไม่ขวางการพิมพ์ ไอคอนใน system tray จะแสดง `EN`, `TH` หรือรหัสภาษาปัจจุบัน ในเกมเต็มจอโปรแกรมจะปิด OSD และใช้ Game-safe mode อัตโนมัติเพื่อไม่รบกวนเมาส์หรือคีย์บอร์ดของเกม

คลิกขวาที่ไอคอนเพื่อเปิดหรือปิดป้ายภาษาบนหน้าจอ และเลือกตำแหน่งค่าเริ่มต้น, มุมซ้ายบน, มุมขวาบน, มุมซ้ายล่าง, กึ่งกลางล่าง หรือมุมขวาล่างได้ เมนูใช้ภาษาไทยเป็นค่าเริ่มต้นและสลับเป็นภาษาอังกฤษได้ โปรแกรมจะจำค่าที่เลือกไว้

ต้องรันแบบ Administrator เพื่อให้สลับภาษาได้ในเกม และในโปรแกรมที่เปิดแบบแอดมิน เช่น PowerShell

โปรแกรมจะทำงานอยู่ที่ system tray มุมขวาล่างของ Windows คลิกขวาที่ไอคอนเพื่อเปิด/ปิดการทำงาน, ตั้งให้เปิดพร้อม Windows, เปิดหน้าตั้งค่าภาษา หรือออกจากโปรแกรม เมื่อปิดการทำงาน ปุ่ม `Caps Lock` จะกลับมาทำงานตามปกติ

ถ้าเปิด `Start with Windows` ขณะที่โปรแกรมรันแบบแอดมิน โปรแกรมจะขึ้นเองตอนล็อกอินโดยไม่ต้องกด UAC ทุกครั้ง

ถ้าต้องการลบโปรแกรม ให้คลิกขวาที่ไอคอนแล้วเอาติ๊ก `Start with Windows` ออก จากนั้นเลือก `Exit` และลบไฟล์ `CapsLangSwitcher.exe`

## ต้องมี

- Windows 10 หรือ Windows 11
- มีภาษาคีย์บอร์ดอย่างน้อย 2 ภาษา เช่น English และ Thai

## Files

- `CapsLangSwitcher.exe` - ไฟล์โปรแกรม ดับเบิลคลิกเพื่อใช้งาน
- `CapsLangSwitcher.cs` - source code
- `app.manifest` - Windows manifest
- `assets/CapsLangSwitcher.png` - ไอคอนโปรแกรม
- `README.md` - วิธีใช้
- `LICENSE` - MIT License

## English

CapsLangSwitcher is a lightweight Windows tray app that uses `Caps Lock` as a keyboard language switch key.

Double-click `CapsLangSwitcher.exe` and accept the Administrator prompt. Press and release `Caps Lock` to switch languages. Hold `Caps Lock` and press `Spacebar` to toggle real Caps Lock without switching language or typing a space. The non-activating on-screen indicator appears briefly without stealing keyboard focus; it is automatically suppressed for full-screen games, where a game-safe system language switch is used instead. Running as Administrator is required so the switch works in games and elevated windows such as admin PowerShell.
