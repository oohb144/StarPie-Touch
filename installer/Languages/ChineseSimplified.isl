; *** Inno Setup version 6.5.0+ Chinese Simplified messages ***
;
; Maintainer: Zhenghan Yang (Kira)
; Email: 847320916@QQ.com
; Github: https://github.com/kira-96/Inno-Setup-Chinese-Simplified-Translation
; Encoding: UTF-8

[LangOptions]
LanguageName=简体中文
LanguageID=$0804
LanguageCodePage=936

[Messages]

; *** Application titles
SetupAppTitle=安装
SetupWindowTitle=安装 - %1
UninstallAppTitle=卸载
UninstallAppFullTitle=%1 卸载

; *** Misc. common
InformationTitle=信息
ConfirmTitle=确认
ErrorTitle=错误

; *** SetupLdr messages
SetupLdrStartupMessage=现在将安装 %1。您想要继续吗？
LdrCannotCreateTemp=无法创建临时文件。安装程序已中止
LdrCannotExecTemp=无法执行临时目录中的文件。安装程序已中止
HelpTextNote=

; *** Startup error messages
LastErrorMessage=%1。%n%n错误 %2: %3
SetupFileMissing=安装目录中缺少文件 %1。请修正这个问题或者获取程序的新副本。
SetupFileCorrupt=安装文件已损坏。请获取程序的新副本。
SetupFileCorruptOrWrongVer=安装文件已损坏，或是与这个安装程序的版本不兼容。请修正这个问题或获取新的程序副本。
InvalidParameter=无效的命令行参数：%n%n%1
SetupAlreadyRunning=安装程序已在运行。
WindowsVersionNotSupported=此程序不支持当前计算机运行的 Windows 版本。
WindowsServicePackRequired=此程序需要 %1 服务包 %2 或更高版本。
NotOnThisPlatform=此程序不能在 %1 上运行。
OnlyOnThisPlatform=此程序只能在 %1 上运行。
OnlyOnTheseArchitectures=此程序只能安装到为下列处理器架构设计的 Windows 版本中：%n%n%1
WinVersionTooLowError=此程序需要 %1 版本 %2 或更高。
WinVersionTooHighError=此程序不能安装于 %1 版本 %2 或更高。
AdminPrivilegesRequired=在安装此程序时您必须以管理员身份登录。
PowerUserPrivilegesRequired=在安装此程序时您必须以管理员身份或高级用户组身份登录。
SetupAppRunningError=安装程序检测到 %1 当前正在运行。%n%n请先关闭正在运行的程序，然后点击“确定”继续，或点击“取消”退出。
UninstallAppRunningError=卸载程序检测到 %1 当前正在运行。%n%n请先关闭正在运行的程序，然后点击“确定”继续，或点击“取消”退出。

; *** Startup questions
PrivilegesRequiredOverrideTitle=选择安装程序安装模式
PrivilegesRequiredOverrideInstruction=选择安装模式
PrivilegesRequiredOverrideText1=%1 可以为所有用户安装（需要管理员权限），或仅为您安装。
PrivilegesRequiredOverrideText2=%1 可以仅为您安装，或为所有用户安装（需要管理员权限）。
PrivilegesRequiredOverrideAllUsers=为所有用户安装(&A)
PrivilegesRequiredOverrideAllUsersRecommended=为所有用户安装(&A)（推荐）
PrivilegesRequiredOverrideCurrentUser=仅为我安装(&M)
PrivilegesRequiredOverrideCurrentUserRecommended=仅为我安装(&M)（推荐）

; *** Misc. errors
ErrorCreatingDir=安装程序无法创建目录“%1”
ErrorTooManyFilesInDir=无法在目录“%1”中创建文件，因为里面包含太多文件。

; *** Setup common messages
ExitSetupTitle=退出安装程序
ExitSetupMessage=安装程序尚未完成。如果现在退出，将不会安装该程序。%n%n您之后可以再次运行安装程序完成安装。%n%n现在退出安装程序吗？
AboutSetupMenuItem=关于安装程序(&A)...
AboutSetupTitle=关于安装程序
AboutSetupMessage=%1 版本 %2%n%3%n%n%1 主页：%n%4
AboutSetupNote=
TranslatorURL=
SelectLanguageTitle=选择安装语言
SelectLanguageLabel=选择在安装时使用的语言。

; *** Common Wizard text
ButtonNext=下一步(&N) >
ButtonBack=< 上一步(&B)
ButtonInstall=安装(&I)
ButtonOK=确定
ButtonCancel=取消
ButtonYes=是(&Y)
ButtonYesToAll=全是(&A)
ButtonNo=否(&N)
ButtonNoToAll=全否(&O)
ButtonFinish=完成(&F)
ButtonBrowse=浏览(&B)...
ButtonWizardBrowse=浏览(&R)...
ButtonNewFolder=新建文件夹(&M)

; *** "Select Language" wizard page
SelectLanguageTitle=选择安装语言
SelectLanguageLabel=选择在安装时使用的语言。

; *** "Welcome" wizard page
WelcomeLabel1=欢迎使用 [name] 安装向导
WelcomeLabel2=现在将安装 [name/ver] 到您的计算机。%n%n建议您在继续之前关闭所有其他应用程序。

; *** "Password" wizard page
WizardPassword=密码
PasswordLabel1=此安装程序受密码保护。
PasswordLabel3=请输入密码，然后点击“下一步”继续。密码区分大小写。
PasswordEditLabel=密码(&P):
IncorrectPassword=您输入的密码不正确。请重试。

; *** "License Agreement" wizard page
WizardLicense=许可协议
LicenseLabel=请在继续之前仔细阅读以下重要信息。
LicenseLabel3=请阅读以下许可协议。在继续安装之前，您必须接受此协议的条款。
LicenseAccepted=我同意此协议(&A)
LicenseNotAccepted=我不同意此协议(&D)

; *** "Information" wizard pages
WizardInfoBefore=信息
InfoBeforeLabel=请在继续之前仔细阅读以下重要信息。
InfoBeforeClickLabel=当您准备好继续安装时，点击“下一步”。
WizardInfoAfter=信息
InfoAfterLabel=请在继续之前仔细阅读以下重要信息。
InfoAfterClickLabel=当您准备好继续安装时，点击“下一步”。

; *** "User Information" wizard page
WizardUserInfo=用户信息
UserInfoDesc=请输入您的信息。
UserInfoName=用户姓名(&U):
UserInfoOrg=所属组织(&O):
UserInfoSerial=序列号(&S):
UserInfoNameRequired=您必须输入一个姓名。

; *** "Select Destination Location" wizard page
WizardSelectDir=选择目标位置
SelectDirDesc=程序将安装到什么地方？
SelectDirLabel3=安装程序将安装 [name] 到下列文件夹中。
SelectDirBrowseLabel=若要继续，请点击“下一步”。如果您想选择其他文件夹，请点击“浏览”。
DiskSpaceMBLabel=至少需要 [mb] MB 的可用磁盘空间。
CannotInstallToNetworkDrive=安装程序无法安装到网络驱动器。
CannotInstallToUNCPath=安装程序无法安装到 UNC 路径。
InvalidPath=您必须输入包含盘符的完整路径；例如：%n%nC:\APP%n%n或者以下形式的 UNC 路径：%n%n\\server\share
InvalidDrive=您选择的驱动器或 UNC 共享不存在或无法访问。请选择其他位置。
DiskSpaceWarningTitle=磁盘空间不足
DiskSpaceWarning=安装程序至少需要 %1 KB 可用空间才能安装，但是选中的驱动器只有 %2 KB 可用。%n%n您想要继续吗？
DirNameTooLong=文件夹名称或路径太长。
InvalidDirName=文件夹名称无效。
BadDirName386=文件夹名称不能包含下列任何字符：%n%n%1
DirExistsTitle=文件夹已存在
DirExists=文件夹：%n%n%1%n%n已存在。您想要安装到这个文件夹中吗？
DirDoesntExistTitle=文件夹不存在
DirDoesntExist=文件夹：%n%n%1%n%n不存在。您想要创建这个文件夹吗？

; *** "Select Components" wizard page
WizardSelectComponents=选择组件
SelectComponentsDesc=应该安装哪些组件？
SelectComponentsLabel2=勾选您想要安装的组件；取消勾选您不想安装的组件。然后点击“下一步”继续。
FullInstallation=完全安装
CompactInstallation=精简安装
CustomInstallation=自定义安装
NoUninstallWarningTitle=组件已存在
NoUninstallWarning=安装程序检测到下列组件已经安装在您的计算机中：%n%n%1%n%n取消勾选这些组件将不会卸载它们。%n%n您想要继续吗？
ComponentSize1=%1 KB
ComponentSize2=%2 MB
ComponentsDiskSpaceMBLabel=当前选择的组件至少需要 [mb] MB 的磁盘空间。

; *** "Select Additional Tasks" wizard page
WizardSelectTasks=选择附加任务
SelectTasksDesc=应该执行哪些附加任务？
SelectTasksLabel2=选择您想要安装程序在安装 [name] 时执行的附加任务，然后点击“下一步”。

; *** "Select Start Menu Folder" wizard page
WizardSelectProgramGroup=选择开始菜单文件夹
SelectStartMenuFolderDesc=应该在哪里放置程序的快捷方式？
SelectStartMenuFolderLabel3=安装程序将在下列开始菜单文件夹中创建程序的快捷方式。
SelectStartMenuFolderBrowseLabel=若要继续，请点击“下一步”。如果您想选择其他文件夹，请点击“浏览”。
MustEnterGroupName=您必须输入一个文件夹名称。
GroupNameTooLong=文件夹名称或路径太长。
InvalidGroupName=文件夹名称无效。
BadGroupName=文件夹名称不能包含下列任何字符：%n%n%1
NoProgramGroupCheck2=不要创建开始菜单文件夹(&D)

; *** "Ready to Install" wizard page
WizardReady=准备安装
ReadyLabel1=安装程序现在准备开始在您的计算机中安装 [name]。
ReadyLabel2a=点击“安装”继续安装，或者点击“上一步”检查或修改任何设置。
ReadyLabel2b=点击“安装”继续安装。
ReadyMemoUserInfo=用户信息：
ReadyMemoDir=目标位置：
ReadyMemoType=安装类型：
ReadyMemoComponents=选定组件：
ReadyMemoGroup=开始菜单文件夹：
ReadyMemoTasks=附加任务：

; *** TDownloadWizardPage wizard page and DownloadTemporaryFile
DownloadingLabel=正在下载附加文件...
ButtonStopDownload=停止下载(&S)
StopDownload=您确定要停止下载吗？
ErrorDownloadAborted=下载已被中止
ErrorDownloadFailed=下载失败: %1 %2
ErrorDownloadSizeFailed=获取文件大小失败: %1 %2
ErrorFileHash1=文件校验失败: %1
ErrorFileHash2=无效的文件校验: 预期 %1，实际 %2
ErrorProgress=无效进度: %1 / %2
ErrorFileSize=无效文件大小: 预期 %1，实际 %2

; *** "Preparing to Install" wizard page
WizardPreparing=正在准备安装
PreparingDesc=安装程序正在准备在您的计算机中安装 [name]。
PreviousInstallNotCompleted=先前程序的安装或卸载尚未完成。您需要重新启动计算机完成该安装。%n%n重新启动计算机后，请重新运行安装程序完成 [name] 的安装。
CannotContinue=安装程序无法继续。请点击“取消”退出。
ApplicationsFound=下列应用程序正在使用需要由安装程序更新的文件。建议您允许安装程序自动关闭这些应用程序。
ApplicationsFound2=下列应用程序正在使用需要由安装程序更新的文件。建议您允许安装程序自动关闭这些应用程序。安装完成后，安装程序将尝试重新启动这些应用程序。
CloseApplications=自动关闭应用程序(&A)
DontCloseApplications=不要关闭应用程序(&D)
ErrorCloseApplications=安装程序无法自动关闭所有应用程序。建议您在继续之前关闭所有使用需要由安装程序更新的文件的应用程序。
PrepareToInstallNeedsRestart=安装程序必须重新启动计算机。重新启动计算机后，请重新运行安装程序完成 [name] 的安装。%n%n您想要现在重新启动吗？

; *** "Installing" wizard page
WizardInstalling=正在安装
InstallingLabel=安装程序正在将 [name] 安装到您的计算机中，请稍候。

; *** "Setup Completed" wizard page
FinishedHeadingLabel=[name] 安装向导完成
FinishedLabelNoIcons=安装程序已在您的计算机中安装了 [name]。
FinishedLabel=安装程序已在您的计算机中安装了 [name]。可以通过选择已安装的图标运行此应用程序。
ClickFinish=点击“完成”退出安装程序。
FinishedRestartLabel=为了完成 [name] 的安装，安装程序必须重新启动您的计算机。您想要现在重新启动吗？
FinishedRestartMessage=为了完成 [name] 的安装，安装程序必须重新启动您的计算机。%n%n您想要现在重新启动吗？
ShowReadmeCheck=是，我想查看自述文件
YesRadio=是，现在重新启动计算机(&Y)
NoRadio=否，稍后重新启动计算机(&N)
ChangeRunAsUser=安装程序将以用户 %1 继续。

; *** "About Setup" dialog box
AboutSetupTitle=关于安装程序
AboutSetupMessage=%1 版本 %2%n%3%n%n%1 主页：%n%4
AboutSetupNote=

; *** Tasks
CreateDesktopIcon=创建桌面快捷方式(&D)
CreateQuickLaunchIcon=创建快速启动快捷方式(&Q)
ProgramOnTheWeb=%1 网站
LaunchProgram=运行 %1(&L)
Assoc=将 %1 与 %2 文件扩展名关联(&A)
Associng=正在将 %1 与 %2 文件扩展名关联...
AutoStartProgramGroupDescription=启动：
AutoStartProgram=自动启动 %1
AddonHostProgramNotFound=%1 无法在您选择的文件夹中找到。%n%n您想要继续吗？

; *** Uninstallation messages
ConfirmUninstall=您确定要完全删除 %1 及其所有组件吗？
UninstallStatusLabel=正在从您的计算机中删除 %1，请稍候。
UninstalledAll=%1 已成功从您的计算机中删除。
UninstalledMost=%1 卸载完成。%n%n某些元素无法删除。这些元素可以手动删除。
UninstalledAndNeedsRestart=为了完成 %1 的卸载，必须重新启动计算机。%n%n您想要现在重新启动吗？
UninstallDataQuestion=是否同时删除所有 StarPie 用户自定义配置与轮盘方案数据？%n%n如果选择“否”，后续重新安装时将自动保留并恢复您的个人配置。
UninstallStatusHeading=正在卸载 %1
UninstallStatusDesc=正在卸载 %1，请稍候...

; *** Uninstallation status messages
StatusClosingApplications=正在关闭应用程序...
StatusCreateRegistryKeys=正在创建注册表项...
StatusDeleteFiles=正在删除文件...
StatusDeleteINIEntries=正在删除 INI 条目...
StatusDeleteRegistryKeys=正在删除注册表项...
StatusDeleteUninstallRunEntries=正在删除卸载运行条目...
StatusExecuteActions=正在执行操作...
StatusExtractFiles=正在提取文件...
StatusPendingUninstall=正在准备卸载...
StatusRegisterFiles=正在注册文件...
StatusSavingINIEntries=正在保存 INI 条目...
StatusUninstallRunEntries=正在运行卸载程序...
StatusUnregisterFiles=正在注销文件...
