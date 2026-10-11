from pathlib import Path
from xml.etree import ElementTree as ET

out = Path(__file__).parent
mark = '''<g id="symbol">
<g fill="#36C4F4">
<path d="M107 36 169 0 300 76 241 110Z"/>
<path d="M0 99 79 53 130 83 59 133Z"/>
<path d="M86 227 104 217 155 246 281 173 300 184 300 193 164 272Z"/>
</g>
<g fill="#00ADEA">
<path d="M0 99 59 133 59 283 0 249Z"/>
<path d="M86 227 164 272 164 344 86 299Z"/>
</g>
<g fill="#006DB5">
<path d="M241 110 300 76 300 157 241 192Z"/>
<path d="M164 272 300 193 300 265 164 344Z"/>
</g>
<g fill="#004B80">
<path d="M107 36 241 110 241 192 222 181 222 122 107 45Z"/>
<path d="M59 133 130 83 130 105 78 142 78 272 59 283Z"/>
</g>
<path fill="#FFD34B" d="M104 145 154 116 204 145 154 174Z"/>
<path fill="#FFB900" d="M104 145 154 174 154 232 104 203Z"/>
<path fill="#E49600" d="M154 174 204 145 204 203 154 232Z"/>
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
word = f'<g id="wordmark" fill="#F2F5F7" fill-rule="evenodd" transform="translate(348 114) scale(1.15)">{letters}</g>'
def svg(body, box):
    return f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="{box}" role="img" aria-labelledby="title"><title id="title">Blockforge</title>{body}</svg>\n'
for name, data in {
    'blockforge-mark.svg': svg(mark, '-12 -12 324 368'),
    'blockforge-logo-horizontal.svg': svg(mark+word, '-12 -12 1232 368'),
}.items():
    ET.fromstring(data)
    (out/name).write_text(data, encoding='utf-8')
    print((out/name).resolve())
