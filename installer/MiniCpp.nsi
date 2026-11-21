Unicode true
!include MUI2.nsh
!include LogicLib.nsh
!include x64.nsh
!include Sections.nsh
!define APP "Mini C++"
!define PROGID "MiniCpp.Source.cpp"
!define CAP "Software\MiniCpp\Capabilities"
!define CLASSES "Software\Classes"
!define UNKEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\MiniCpp.Dev"
!define STARTLINK "$SMPROGRAMS\Mini C++.lnk"
!define DESKTOPLINK "$DESKTOP\Mini C++.lnk"
Name "Mini C++ ${APPCHANNEL} v${APPVERSION}"
OutFile "${OUTPUT}"
InstallDir "$LOCALAPPDATA\Programs\MiniCpp"
InstallDirRegKey HKCU "${UNKEY}" "InstallLocation"
RequestExecutionLevel user
SetCompressor /SOLID lzma
SetCompressorDictSize 32
ManifestDPIAware true
!define MUI_ICON "${APPDIR}\assets\mini-cpp.ico"
!define MUI_UNICON "${APPDIR}\assets\mini-cpp.ico"
VIProductVersion ${APPVERSION}.0
VIAddVersionKey "ProductName" "Mini C++"
VIAddVersionKey "FileDescription" "Mini C++ ${APPCHANNEL} v${APPVERSION} Setup"
VIAddVersionKey "FileVersion" "${APPVERSION}.0"
VIAddVersionKey "ProductVersion" "${PRODUCTVERSION}"
VIAddVersionKey "LegalCopyright" "Mini C++ contributors"
!define MUI_ABORTWARNING
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN "$INSTDIR\MiniCpp.exe"
!define MUI_FINISHPAGE_RUN_NOTCHECKED
!define MUI_FINISHPAGE_TEXT "Mini C++ ${APPCHANNEL} v${APPVERSION} 已安装完成。$\r$\n$\r$\n要固定到「开始」，可勾选下方选项，完成后右键单击选中的快捷方式。"
!define MUI_FINISHPAGE_SHOWREADME
!define MUI_FINISHPAGE_SHOWREADME_TEXT "查看开始菜单快捷方式"
!define MUI_FINISHPAGE_SHOWREADME_NOTCHECKED
!define MUI_FINISHPAGE_SHOWREADME_FUNCTION ShowStartMenuShortcut
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "SimpChinese"

Var ConfigMatches

Function .onInit
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP "Mini C++ 需要 64 位 Windows。"
    Abort
  ${EndIf}
  SetRegView 64
  SetShellVarContext current
FunctionEnd

Function ShowStartMenuShortcut
  ExecShell "open" "$WINDIR\explorer.exe" '/select,$\"${STARTLINK}$\"'
FunctionEnd

; Skip rewriting an identical config, even if another process has it open for reading.
Function CompareAppConfig
  StrCpy $ConfigMatches 0
  ClearErrors
  FileOpen $0 "$INSTDIR\MiniCpp.exe.config" r
  IfErrors done
  FileOpen $1 "$PLUGINSDIR\MiniCpp.exe.config" r
  IfErrors closeInstalled
  FileSeek $0 0 END $2
  FileSeek $1 0 END $3
  IfErrors closeBoth
  IntCmp $2 $3 0 closeBoth closeBoth
  FileSeek $0 0 SET
  FileSeek $1 0 SET
  IfErrors closeBoth
  compareByte:
    IntCmp $2 0 identical
    FileReadByte $0 $3
    FileReadByte $1 $4
    IfErrors closeBoth
    StrCmp $3 $4 0 closeBoth
    IntOp $2 $2 - 1
    Goto compareByte
  identical:
    StrCpy $ConfigMatches 1
  closeBoth:
    FileClose $1
  closeInstalled:
    FileClose $0
  done:
FunctionEnd

; $0 is a package destination. Probe the same write access and sharing mode as File.
Function CheckInstallFile
  retry:
    System::Call 'kernel32::GetFileAttributesW(w r0) i.r1 ?e'
    Pop $2
    ${If} $1 == -1
      ${If} $2 == 2
      ${OrIf} $2 == 3
        Return
      ${EndIf}
      Goto blocked
    ${EndIf}
    IntOp $2 $1 & 1
    ${If} $2 != 0
      IntOp $3 $1 & 0xfffffffe
      ${If} $3 == 0
        StrCpy $3 0x80
      ${EndIf}
      System::Call 'kernel32::SetFileAttributesW(w r0, i r3) i.r3'
      StrCmp $3 0 blocked
    ${EndIf}
    System::Call 'kernel32::CreateFileW(w r0, i 0x40000000, i 1, p 0, i 3, i 0, p 0) p.r3'
    ${If} $2 != 0
      System::Call 'kernel32::SetFileAttributesW(w r0, i r1) i.r4'
    ${EndIf}
    StrCmp $3 -1 blocked
    System::Call 'kernel32::CloseHandle(p r3)'
    Return
  blocked:
    MessageBox MB_RETRYCANCEL|MB_ICONEXCLAMATION "安装程序无法更新以下文件：$\r$\n$0$\r$\n$\r$\n请关闭 Mini C++ 和正在使用该文件的程序，确认当前用户有写入权限，再点击重试。" /SD IDCANCEL IDRETRY retry
    SetErrorLevel 5
    Abort
FunctionEnd

Section "Mini C++ 和 C++ 工具链（必选）" IDE
  SectionIn RO
  InitPluginsDir
  SetOutPath "$PLUGINSDIR"
  File "${APPDIR}\MiniCpp.exe.config"
  Call CompareAppConfig
  ${If} $ConfigMatches == 0
    StrCpy $0 "$INSTDIR\MiniCpp.exe.config"
    Call CheckInstallFile
  ${EndIf}
  StrCpy $0 "$INSTDIR\Uninstall.exe"
  Call CheckInstallFile
  !include "${CHECKFILES}"

  ClearErrors
  SetOutPath "$INSTDIR"
  IfErrors installFailed
  GetTempFileName $0 "$INSTDIR"
  IfErrors installFailed
  Delete "$0"
  SetOverwrite on
  AllowSkipFiles off
  ${If} $ConfigMatches == 0
    File "${APPDIR}\MiniCpp.exe.config"
  ${EndIf}
  File /r /x "MiniCpp.exe.config" "${APPDIR}\*.*"
  IfErrors installFailed
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  IfErrors installFailed
  SetOutPath "$INSTDIR"
  CreateShortcut "${STARTLINK}" "$INSTDIR\MiniCpp.exe"
  WriteRegStr HKCU "${UNKEY}" "DisplayName" "Mini C++ ${APPCHANNEL}"
  WriteRegStr HKCU "${UNKEY}" "DisplayVersion" "${PRODUCTVERSION}"
  WriteRegStr HKCU "${UNKEY}" "Publisher" "Mini C++"
  WriteRegStr HKCU "${UNKEY}" "DisplayIcon" "$INSTDIR\MiniCpp.exe,0"
  WriteRegStr HKCU "${UNKEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNKEY}" "UninstallString" '$\"$INSTDIR\Uninstall.exe$\"'
  WriteRegDWORD HKCU "${UNKEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNKEY}" "NoRepair" 1
  Goto installed
  installFailed:
    MessageBox MB_ICONSTOP "安装未完成。请关闭 Mini C++，确认当前用户可以写入 $INSTDIR 后重新安装。" /SD IDOK
    SetErrorLevel 5
    Abort
  installed:
SectionEnd

Section "创建桌面快捷方式" DESKTOP_SHORTCUT
  SetOutPath "$INSTDIR"
  CreateDirectory "$DESKTOP"
  CreateShortcut "${DESKTOPLINK}" "$INSTDIR\MiniCpp.exe"
SectionEnd

; An optional NSIS section is selected by default unless declared /o.
Section "将 Mini C++ 设为 .cpp 文件的默认打开方式" ASSOC
  WriteRegStr HKCU "${CLASSES}\${PROGID}" "" "C++ 源文件"
  WriteRegStr HKCU "${CLASSES}\${PROGID}\DefaultIcon" "" '$\"$INSTDIR\assets\cpp-file.ico$\",0'
  WriteRegStr HKCU "${CLASSES}\${PROGID}\shell\open\command" "" '$\"$INSTDIR\MiniCpp.exe$\" $\"%1$\"'
  WriteRegStr HKCU "${CLASSES}\.cpp\OpenWithProgids" "${PROGID}" ""
  WriteRegStr HKCU "${CAP}" "ApplicationName" "Mini C++"
  WriteRegStr HKCU "${CAP}" "ApplicationDescription" "C++ 单文件竞赛编辑器"
  WriteRegStr HKCU "${CAP}" "ApplicationIcon" "$INSTDIR\MiniCpp.exe,0"
  WriteRegStr HKCU "${CAP}\FileAssociations" ".cpp" "${PROGID}"
  WriteRegStr HKCU "Software\RegisteredApplications" "MiniCpp" "${CAP}"
  System::Call 'shell32::SHChangeNotify(i 0x08000000, i 0, p 0, p 0)'
SectionEnd

Function .onInstSuccess
  ${If} ${SectionIsSelected} ${ASSOC}
    IfSilent done
    ReadRegStr $0 HKCU "Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.cpp\UserChoice" "ProgId"
    ${If} $0 != "${PROGID}"
      MessageBox MB_YESNO|MB_ICONQUESTION "已注册 Mini C++。是否现在打开 Windows 的默认应用设置，确认用它打开 .cpp 文件？" IDNO done
      ExecShell "open" "ms-settings:defaultapps?registeredAppUser=MiniCpp"
      IfErrors 0 done
      ExecShell "open" "ms-settings:defaultapps"
    ${EndIf}
  ${EndIf}
  done:
FunctionEnd

Section "Uninstall"
  SetRegView 64
  SetShellVarContext current
  ReadRegStr $0 HKCU "${CLASSES}\${PROGID}\shell\open\command" ""
  ${If} $0 == '$\"$INSTDIR\MiniCpp.exe$\" $\"%1$\"'
    DeleteRegValue HKCU "${CLASSES}\.cpp\OpenWithProgids" "${PROGID}"
    DeleteRegKey HKCU "${CLASSES}\${PROGID}"
    DeleteRegValue HKCU "Software\RegisteredApplications" "MiniCpp"
    DeleteRegKey HKCU "${CAP}"
    System::Call 'shell32::SHChangeNotify(i 0x08000000, i 0, p 0, p 0)'
  ${EndIf}
  ReadRegStr $0 HKCU "${UNKEY}" "InstallLocation"
  ${If} $0 == "$INSTDIR"
    DeleteRegKey HKCU "${UNKEY}"
    Delete "${STARTLINK}"
    Delete "${DESKTOPLINK}"
  ${EndIf}
  !include "${UNFILES}"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
SectionEnd

!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${IDE} "安装编辑器、GCC 和 GDB，不更改系统 PATH。"
  !insertmacro MUI_DESCRIPTION_TEXT ${DESKTOP_SHORTCUT} "在当前用户桌面添加 Mini C++ 快捷方式。"
  !insertmacro MUI_DESCRIPTION_TEXT ${ASSOC} "仅影响当前用户。Windows 可能需要你确认默认应用，不会修改 .cpp 文件的内容。"
!insertmacro MUI_FUNCTION_DESCRIPTION_END
