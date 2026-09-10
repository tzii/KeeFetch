"""Build the dependency-free KeeFetch site: python site/build.py."""
from pathlib import Path
import shutil
import tempfile

ROOT = Path(__file__).resolve().parent
OUTPUT = ROOT / 'dist'


def build():
    # Only replace this script's own output, never a linked or external path.
    if OUTPUT.is_symlink() or OUTPUT.resolve().parent != ROOT:
        raise SystemExit('Refusing to replace an output outside the website.')
    pages = sorted(ROOT.glob('*.html'))
    if len(pages) != 7 or not (ROOT / 'index.html').is_file():
        raise SystemExit('Expected the complete seven-page website.')
    with tempfile.TemporaryDirectory(prefix='.site-build-', dir=ROOT) as temporary:
        stage = Path(temporary)
        for page in pages:
            shutil.copy2(page, stage / page.name)
        if (ROOT / '.nojekyll').is_file():
            shutil.copy2(ROOT / '.nojekyll', stage / '.nojekyll')
        for directory in ('assets', 'data'):
            shutil.copytree(ROOT / directory, stage / directory)
        if OUTPUT.exists():
            shutil.rmtree(OUTPUT)
        shutil.copytree(stage, OUTPUT)
    files = [p for p in OUTPUT.rglob('*') if p.is_file()]
    print(f'Built {len(pages)} pages and {len(files)} files into {OUTPUT}')


if __name__ == '__main__':
    build()
