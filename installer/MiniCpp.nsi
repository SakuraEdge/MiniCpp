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
Name "Mini C++ Dev v${APPVERSION}"
OutFile "${OUTPUT}"
InstallDir "$LOCALAPPDATA\Programs\MiniCpp"
RequestExecutionLevel user
SetCompressor /SOLID lzma
SetCompressorDictSize 32
ManifestDPIAware true
Icon "${APPDIR}\assets\mini-cpp.ico"
UninstallIcon "${APPDIR}\assets\mini-cpp.ico"
VIProductVersion ${APPVERSION}.0
VIAddVersionKey "ProductName" "Mini C++"
VIAddVersionKey "FileDescription" "Mini C++ Dev v${APPVERSION} Setup"
VIAddVersionKey "FileVersion" "${APPVERSION}.0"
VIAddVersionKey "LegalCopyright" "Mini C++ contributors"
!define MUI_ABORTWARNING
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN "$INSTDIR\MiniCpp.exe"
!define MUI_FINISHPAGE_RUN_NOTCHECKED
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "SimpChinese"

Function .onInit
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP "Mini C++ 需要 64 位 Windows。"
    Abort
  ${EndIf}
  SetRegView 64
  SetShellVarContext current
FunctionEnd

Section "Mini C++ 和 C++ 工具链（必选）" IDE
  SectionIn RO
  SetOutPath "$INSTDIR"
  SetOverwrite on
  ClearErrors
  File /r "${APPDIR}\*.*"
  IfErrors 0 +3
    MessageBox MB_ICONSTOP "安装未完成。请关闭正在使用的 Mini C++ 后重新安装。"
    Abort
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  CreateShortcut "$SMPROGRAMS\Mini C++.lnk" "$INSTDIR\MiniCpp.exe"
  WriteRegStr HKCU "${UNKEY}" "DisplayName" "Mini C++ Dev"
  WriteRegStr HKCU "${UNKEY}" "DisplayVersion" "${APPVERSION}-dev"
  WriteRegStr HKCU "${UNKEY}" "Publisher" "Mini C++"
  WriteRegStr HKCU "${UNKEY}" "DisplayIcon" "$INSTDIR\MiniCpp.exe,0"
  WriteRegStr HKCU "${UNKEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNKEY}" "UninstallString" '$\"$INSTDIR\Uninstall.exe$\"'
  WriteRegDWORD HKCU "${UNKEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNKEY}" "NoRepair" 1
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
  DeleteRegKey HKCU "${UNKEY}"
  Delete "$SMPROGRAMS\Mini C++.lnk"
  !include "${UNFILES}"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
SectionEnd

!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${IDE} "安装编辑器、GCC 和 GDB，不更改系统 PATH。"
  !insertmacro MUI_DESCRIPTION_TEXT ${ASSOC} "仅影响当前用户。Windows 可能需要你确认默认应用，不会修改 .cpp 文件的内容。"
!insertmacro MUI_FUNCTION_DESCRIPTION_END
