"""Package the actual concept as one offline-capable HTML file (no font files)."""
from pathlib import Path
import argparse
import base64
import re


def package(source: Path) -> str:
    html = (source / 'index.html').read_text(encoding='utf-8')
    css = (source / 'style.css').read_text(encoding='utf-8')
    js = (source / 'app.js').read_text(encoding='utf-8')
    html = html.replace('<link rel="stylesheet" href="style.css">', '<style>\n' + css + '\n</style>')
    html = html.replace('<script src="app.js"></script>', '<script>\n' + js + '\n</script>')
    logo = source.parent / 'assets/icons/keefetch-app.png'
    data = 'data:image/png;base64,' + base64.b64encode(logo.read_bytes()).decode('ascii')
    html = html.replace('../assets/icons/keefetch-app.png', data)
    for name in ('github','figma','notion','spotify'):
        svg = (source / f'brands/{name}.svg').read_bytes()
        data = 'data:image/svg+xml;base64,' + base64.b64encode(svg).decode('ascii')
        html = html.replace(f'brands/{name}.svg', data)
    return html


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    args.output.write_text(package(Path(__file__).resolve().parent), encoding='utf-8')
    print(f'Packaged {args.output} ({args.output.stat().st_size:,} bytes)')
