# Project Eternal: TF2 Launcher

![Build Status](https://img.shields.io/badge/build-passing-brightgreen)
![Platform](https://img.shields.io/badge/platform-Windows-blue)
![License](https://img.shields.io/badge/license-MIT-green)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)

A modern launcher for Team Fortress 2, designed to enhance your gaming experience with advanced configuration options, mod management, and rich presence integration.

## ✨ Features

- **Modern UI**: Sleek, tabbed interface with a unique aesthetic.
- **System Tray Integration**: Minimize to tray for background operation.
- **Rich Presence**: Custom Discord RPC to show your status.
- **Mod Management**: Easily manage your TF2 mods.
- **Advanced Settings**: Configure `autoexec`, launch options, and hidden game settings.
- **Optimization**: Tools to optimize game performance.

## 📸 Screenshots

![Home Tab](resources/Assets/backrnd1.png)
*(Placeholder for actual application screenshots)*

## 🚀 Getting Started

### Prerequisites

- **Windows 10/11**
- **.NET 8.0 Desktop Runtime**: [Download here](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- **Visual Studio 2022** (for development)

### Build Instructions

1.  Clone the repository:
    ```bash
    git clone https://github.com/yourusername/project_eternal_launcher.git
    ```
2.  Navigate to the project root.
3.  Run the build script:
    ```powershell
    ./scripts/build.ps1
    ```
    Or manually:
    ```bash
    dotnet build src/LauncherTF2/LauncherTF2.csproj
    ```

### How to Run

Run the launch script:
```bat
scripts\run.bat
```
Or open `src/LauncherTF2.sln` in Visual Studio and press **F5**.

## 🤝 Contributing

Contributions are welcome! Please follow these steps:

1.  Fork the repository.
2.  Create a feature branch (`git checkout -b feature/AmazingFeature`).
3.  Commit your changes (`git commit -m 'Add some AmazingFeature'`).
4.  Push to the branch (`git push origin feature/AmazingFeature`).
5.  Open a Pull Request.

## 📄 License

Distributed under the MIT License. See `LICENSE` for more information.
