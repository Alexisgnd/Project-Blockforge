"""
Genere les assets bitmap du design system Blockforge pour Unity :
- Shapes/ : rectangles arrondis 9-slice (fond, contour 1 px, anneau de focus
  2 px decale de 4 px, contour pointille), ombres portees ;
- Icons/  : les 70 pictogrammes SVG rasterises en blanc (teintes dans Unity) ;
- Logo/   : lockup horizontal, mark.
Les reglages d'import (sprite, bordures 9-slice, pixels par unite) sont poses
par DesignSystemAssetImporter d'apres le nom : _k<facteur> _b<bordure>.

Usage : python build_ds_assets.py <dossier design-system> <dossier Resources/DesignSystem>
"""
import os, sys, glob
import numpy as np
from PIL import Image, ImageFilter

K = 4  # sur-echantillonnage : 1 px canvas = 4 texels

def rrect_sd(w, h, r, inset=0.0):
    """Distance signee (texels) au bord d'un rectangle arrondi w x h, rayon r, retreci de inset."""
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float64) + 0.5
    cx, cy = w / 2.0, h / 2.0
    hx, hy = w / 2.0 - inset, h / 2.0 - inset
    rr = max(r - inset, 0.0)
    qx = np.abs(xs - cx) - (hx - rr)
    qy = np.abs(ys - cy) - (hy - rr)
    outside = np.hypot(np.maximum(qx, 0), np.maximum(qy, 0))
    inside = np.minimum(np.maximum(qx, qy), 0)
    return outside + inside - rr

def cov(sd):
    return np.clip(0.5 - sd, 0.0, 1.0)

def save(alpha, path):
    a = (np.clip(alpha, 0, 1) * 255 + 0.5).astype(np.uint8)
    rgba = np.zeros(a.shape + (4,), np.uint8)
    rgba[..., :3] = 255
    rgba[..., 3] = a
    Image.fromarray(rgba, 'RGBA').save(path)

def shapes(out):
    os.makedirs(out, exist_ok=True)
    radii = {'xs': 2, 'sm': 3, 'md': 6, 'lg': 10, 'pill': 14}
    for name, r in radii.items():
        # Fond + contour 1 px : meme geometrie
        B = (r + 2) * K
        S = 2 * B + 2 * K
        sd = rrect_sd(S, S, r * K)
        save(cov(sd), f'{out}/{name}_fill_k{K}_b{B}.png')
        save(cov(sd) - cov(sd + 1 * K), f'{out}/{name}_stroke_k{K}_b{B}.png')
        # Anneau de focus : 2 px, decale de 4 px (space-1) hors du controle.
        # Le sprite couvre le controle + 6 px de chaque cote.
        rb = r + 6
        B2 = (rb + 2) * K
        S2 = 2 * B2 + 2 * K
        sd2 = rrect_sd(S2, S2, rb * K)
        save(cov(sd2) - cov(sd2 + 2 * K), f'{out}/{name}_ring_k{K}_b{B2}.png')
    # Contour pointille (RobotSlot vide) : image Tiled, periode 8 px (4 trait / 4 vide)
    r = radii['lg']
    B = (r + 2) * K
    P = 8 * K
    S = 2 * B + P
    sd = rrect_sd(S, S, r * K)
    ring = cov(sd) - cov(sd + 1 * K)
    ys, xs = np.mgrid[0:S, 0:S]
    phase_x = ((xs - B) % P) < (P // 2)
    phase_y = ((ys - B) % P) < (P // 2)
    in_corner_x = (xs < r * K) | (xs >= S - r * K)
    in_corner_y = (ys < r * K) | (ys >= S - r * K)
    corner = in_corner_x & in_corner_y
    horizontal_edge = ~in_corner_x  # bords haut / bas
    vertical_edge = ~in_corner_y    # bords gauche / droit
    mask = corner | (horizontal_edge & phase_x) | (vertical_edge & phase_y)
    save(ring * mask, f'{out}/lg_dash_k{K}_b{B}.png')
    # Ombres (k=1, floues) : shadow-panel 0 2 10 / shadow-overlay 0 16 44
    for name, r, blur in (('shadow_panel', 6, 10), ('shadow_overlay', 10, 44)):
        sigma = blur / 2.0
        pad = int(np.ceil(3 * sigma))
        core = 2 * r + 4
        S = core + 2 * pad
        img = np.zeros((S, S))
        sd = rrect_sd(core, core, r)
        img[pad:pad + core, pad:pad + core] = cov(sd)
        pil = Image.fromarray((img * 255).astype(np.uint8), 'L').filter(ImageFilter.GaussianBlur(sigma))
        save(np.asarray(pil) / 255.0, f'{out}/{name}_k1_b{pad + r + 2}_p{pad}.png')

def icons(src, out, size=96):
    import cairosvg
    os.makedirs(out, exist_ok=True)
    n = 0
    for svg in sorted(glob.glob(os.path.join(src, '*.svg'))):
        name = os.path.splitext(os.path.basename(svg))[0]
        cairosvg.svg2png(url=svg, write_to=f'{out}/{name}.png', output_width=size, output_height=size)
        n += 1
    return n

def logo(src, out):
    import cairosvg
    os.makedirs(out, exist_ok=True)
    for name, h in (('logo-horizontal', 128), ('mark', 128), ('wordmark', 96)):
        svg = os.path.join(src, name + '.svg')
        if os.path.exists(svg):
            cairosvg.svg2png(url=svg, write_to=f'{out}/{name}.png', output_height=h)

if __name__ == '__main__':
    ds, res = sys.argv[1], sys.argv[2]
    shapes(os.path.join(res, 'Shapes'))
    print('icones :', icons(os.path.join(ds, 'assets', 'Icons'), os.path.join(res, 'Icons')))
    logo(os.path.join(ds, 'assets', 'Logo'), os.path.join(res, 'Logo'))
