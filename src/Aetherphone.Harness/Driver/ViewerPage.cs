namespace Aetherphone.Harness.Driver;

internal static class ViewerPage
{
    public const string Html = """
        <!doctype html>
        <html>
        <head>
        <meta charset="utf-8">
        <title>Aetherphone</title>
        <style>
        html, body { margin: 0; height: 100%; background: #15161a; overflow: hidden; }
        #phone { display: block; margin: 0 auto; height: 100vh; cursor: crosshair; }
        #status { position: fixed; left: 10px; bottom: 8px; color: #8a8f9a; font: 12px system-ui, sans-serif; }
        </style>
        </head>
        <body>
        <canvas id="phone"></canvas>
        <div id="status">connecting</div>
        <script>
        const canvas = document.getElementById('phone');
        const context = canvas.getContext('2d');
        const status = document.getElementById('status');
        let width = 0, height = 0, frames = 0, started = performance.now(), lastMove = 0;
        function send(query) { fetch('/event?' + query, { keepalive: true }).catch(() => {}); }
        function point(event) {
            const rect = canvas.getBoundingClientRect();
            const x = (event.clientX - rect.left) * width / rect.width;
            const y = (event.clientY - rect.top) * height / rect.height;
            return 'x=' + x.toFixed(1) + '&y=' + y.toFixed(1);
        }
        function button(event) { return event.button === 2 ? 1 : event.button === 1 ? 2 : 0; }
        canvas.addEventListener('mousemove', event => {
            const now = performance.now();
            if (now - lastMove < 16) { return; }
            lastMove = now;
            send('kind=move&' + point(event));
        });
        canvas.addEventListener('mousedown', event => {
            event.preventDefault();
            send('kind=move&' + point(event));
            send('kind=button&button=' + button(event) + '&down=1');
        });
        canvas.addEventListener('mouseup', event => {
            event.preventDefault();
            send('kind=button&button=' + button(event) + '&down=0');
        });
        canvas.addEventListener('mouseleave', () => send('kind=leave'));
        canvas.addEventListener('contextmenu', event => event.preventDefault());
        canvas.addEventListener('wheel', event => {
            event.preventDefault();
            send('kind=wheel&dx=' + (-event.deltaX / 100).toFixed(2) + '&dy=' + (-event.deltaY / 100).toFixed(2));
        }, { passive: false });
        window.addEventListener('keydown', event => {
            if (event.key.length === 1 && !event.ctrlKey && !event.metaKey) {
                send('kind=text&text=' + encodeURIComponent(event.key));
            }
            send('kind=key&name=' + encodeURIComponent(event.key) + '&down=1');
            if (!event.metaKey && !event.ctrlKey && event.key !== 'F5') { event.preventDefault(); }
        });
        window.addEventListener('keyup', event => send('kind=key&name=' + encodeURIComponent(event.key) + '&down=0'));
        function loop() {
            const image = new Image();
            image.onload = () => {
                if (image.width !== width || image.height !== height) {
                    width = image.width; height = image.height;
                    canvas.width = width; canvas.height = height;
                }
                context.drawImage(image, 0, 0);
                frames += 1;
                const seconds = (performance.now() - started) / 1000;
                if (seconds >= 1) {
                    status.textContent = (frames / seconds).toFixed(0) + ' fps · ' + width + 'x' + height + ' · mouse and keyboard go to the phone';
                    frames = 0; started = performance.now();
                }
                loop();
            };
            image.onerror = () => { status.textContent = 'driver not reachable, retrying'; setTimeout(loop, 500); };
            image.src = '/frame?t=' + Date.now();
        }
        loop();
        </script>
        </body>
        </html>
        """;
}
