using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;

namespace LauncherTF2.Views;

public partial class InventoryView : UserControl
{
    public InventoryView()
    {
        InitializeComponent();
        InitializeAsync();
    }

    private async void InitializeAsync()
    {
        await Browser.EnsureCoreWebView2Async();

        string script = @"
            (function() {
                var style = document.createElement('style');
                style.innerHTML = `
                    img[src*='signinthroughsteam'],
                    a[href*='steamcommunity.com/openid'],
                    .steam-login-button,
                    [class*='steam-login'] {
                        display: none !important;
                        visibility: hidden !important;
                        opacity: 0 !important;
                        pointer-events: none !important;
                    }
                `;
                document.head.appendChild(style);

                function removeSteamText() {
                    var images = document.querySelectorAll(""img[src*='signinthroughsteam']"");
                    images.forEach(img => img.style.display = 'none');

                    var links = document.querySelectorAll(""a[href*='steamcommunity.com/openid']"");
                    links.forEach(link => link.style.display = 'none');

                    var allElements = document.querySelectorAll('div, span, a, p, button');
                    for (var i = 0; i < allElements.length; i++) {
                        var el = allElements[i];
                        if (el.shadowRoot) continue;
                        var text = el.innerText || '';
                        if (text.includes('Sign in through STEAM') && text.includes('This site not associated')) {
                            el.style.display = 'none';
                        }
                    }
                }
                
                removeSteamText();
                
                window.addEventListener('DOMContentLoaded', removeSteamText);
                
                window.addEventListener('load', removeSteamText);

                var observer = new MutationObserver(function(mutations) {
                    removeSteamText();
                });
                observer.observe(document.documentElement, { childList: true, subtree: true });
                
                setInterval(removeSteamText, 1000);
            })();
        ";

        await Browser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(script);
    }

    private void Back_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (Browser.CanGoBack)
        {
            Browser.GoBack();
        }
    }

    private void Forward_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (Browser.CanGoForward)
        {
            Browser.GoForward();
        }
    }
}
