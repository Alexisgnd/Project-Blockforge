from pathlib import Path
from xml.etree import ElementTree as ET

out = Path(__file__).parent
mark = '''<g id="symbol">
<g fill="#00B9EE">
<path d="M128 0 0 74 0 211 55 179 55 106 128 64Z"/>
<path d="M172 0 300 74 300 211 245 179 245 106 172 64Z"/>
<path d="M77 218 20 251 150 326 280 251 223 218 150 260Z"/>
</g>
<path fill="#FFD34B" d="M150 94 206 126 206 190 150 222 94 190 94 126Z"/>
</g>'''
glyph = {
'B':'M0 0H49L64 15V40L54 50 64 60V85L49 100H0Z M23 21V39H39V21Z M23 61V79H39V61Z',
'L':'M0 0H24V77H64V100H0Z',
'O':'M14 0H50L64 14V86L50 100H14L0 86V14Z M24 23V77H40V23Z',
'C':'M14 0H64V26H24V74H64V100H14L0 86V14Z',
'K':'M0 0H24V38L42 0H67L42 49 69 100H42L24 62V100H0Z',
'F':'M0 0H64V24H24V42H56V66H24V100H0Z',
'R':'M0 0H49L64 15V48L49 62 69 100H42L24 65V100H0Z M24 22V43H40V22Z',
'G':'M14 0H50L64 14V30H40V23H24V77H40V62H32V43H64V86L50 100H14L0 86V14Z',
'E':'M0 0H64V23H24V39H56V61H24V77H64V100H0Z',
}
letters = ''.join(f'<path transform="translate({i*76} 0)" d="{glyph[c]}"/>' for i,c in enumerate('BLOCKFORGE'))
word = f'<g id="wordmark" fill="#F2F5F7" fill-rule="evenodd" transform="translate(348 105.5) scale(1.15)">{letters}</g>'
def svg(body, box):
    return f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="{box}" role="img" aria-labelledby="title"><title id="title">Blockforge</title>{body}</svg>\n'
for name, data in {
    'blockforge-mark.svg': svg(mark, '-12 -12 324 350'),
    'blockforge-logo-horizontal.svg': svg(mark+word, '-12 -12 1232 350'),
}.items():
    ET.fromstring(data)
    (out/name).write_text(data, encoding='utf-8')
    print((out/name).resolve())

if __name__ == '__main__':
    import cairosvg
    for name, width, height in [
        ('blockforge-logo-horizontal', 3696, 1050),
        ('blockforge-mark', 1296, 1400),
    ]:
        cairosvg.svg2png(url=str(out/(name+'.svg')), write_to=str(out/(name+'.png')),
                        output_width=width, output_height=height)
    cairosvg.svg2png(url=str(out/'blockforge-logo-horizontal.svg'),
                    write_to=str(out/'preview.png'), background_color='#101923',
                    output_width=1232, output_height=350)
