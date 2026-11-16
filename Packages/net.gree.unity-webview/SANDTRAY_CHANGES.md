Vendored from https://github.com/gree/unity-webview at commit
950cfbac14dbbf72e87548e77ef11ecc8843988e (zlib license).

Sandtray changes: unity-webview.asmdef explicitly references UnityEngine.UI and
omits the optional new Input System reference because this project uses Unity's
legacy input system. Native source and binaries are unchanged from upstream.

Included platforms: Android, iOS, macOS (Intel/Apple Silicon), Windows x86/x64.
Windows requires Microsoft's WebView2 Runtime on the target computer.

macOS keyboard correction: WebViewObject forwards Command-key combinations and
navigation keys with their modifiers to `_CWebViewPlugin_SendEditingKey`.
The native responder handles copy/cut/paste/select-all (including PNG screenshot
paste); other editing keys reach WKWebView as actual key-down/up events.
Duplicate IMGUI/Input events are suppressed within the same frame. Regular
character input remains on the original text path.

The modified native source and Xcode project are retained in `Native~/Mac`.
The macOS binary was rebuilt for x86_64 + arm64 and ad-hoc signed. Distribution
build signing still uses the app's normal Developer ID/App Store identity.
