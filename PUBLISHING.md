# Publishing To GitHub

## 1. Create The Repository

Recommended repository name:

`CapsLangSwitcher`

Recommended description:

`Use Caps Lock as a Windows keyboard language switch key. Great for Thai/English typing.`

Recommended topics:

`windows`, `keyboard`, `keyboard-layout`, `capslock`, `language-switcher`, `thai-keyboard`, `tray-app`, `csharp`

## 2. Upload The Code

If Git is installed:

```powershell
git init
git add .
git commit -m "Initial release"
git branch -M main
git remote add origin https://github.com/YOUR_USERNAME/CapsLangSwitcher.git
git push -u origin main
```

If Git is not installed, create a new repository on GitHub and use **Add file > Upload files** to upload the files in this folder.

## 3. Create A Release

1. Run `BuildRelease.ps1`
2. Open the GitHub repository
3. Go to **Releases > Draft a new release**
4. Tag: `v1.0.0`
5. Title: `CapsLangSwitcher 1.0.0`
6. Upload `release\CapsLangSwitcher.zip`
7. Publish the release

## 4. Make It Easier To Find

- Add the recommended topics in the repository settings
- Put a short demo GIF or screenshot at the top of `README.md`
- Share the release link in Thai Windows/Facebook/Reddit communities
- Use the words "Caps Lock เปลี่ยนภาษา", "เปลี่ยนภาษา Windows", and "Thai English keyboard" in posts
