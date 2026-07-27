# Harbor Library prototype

An isolated interactive UI prototype for the private Harbor Market workspace. The app uses Tauri 2,
React, TypeScript, and mock data only.

## Run the web prototype

```powershell
pnpm install
pnpm dev
```

Open `http://127.0.0.1:1420`.

## Run as a Tauri app

Install the Rust toolchain, the Windows MSVC build tools, and WebView2, then run:

```powershell
pnpm tauri dev
```

The current prototype intentionally has no AI, authentication, analytics, backend, or persistence.
