# =========================================================
# CHAMP D'ETOILES DU GARAGE (dome spatial)
# Usage : python generate_starfield.py   -> out/SpaceStarfield_Dome.png
# Projection equirectangulaire 4096 x 2048 pour la sphere SpaceStarDome
# (Blockforge > Setup Garage Space Sky) : fond noir, ~10 000 etoiles
# reparties uniformement sur la sphere, etirees en largeur selon la
# latitude pour rester rondes une fois plaquees. Graine fixe (1977).
# Dependances : numpy, pillow
# =========================================================
import os
os.makedirs('out', exist_ok=True)
import numpy as np
from PIL import Image
rng=np.random.default_rng(1977)
W,H=4096,2048
img=np.zeros((H,W,3),np.float32)
def add(n,b_lo,b_hi,s_lo,s_hi):
    v=rng.normal(size=(n,3)); v/=np.linalg.norm(v,axis=1,keepdims=True)
    lon=np.arctan2(v[:,0],v[:,2]); lat=np.arcsin(v[:,1])
    u=(lon/(2*np.pi)+0.5)*W; w=(0.5-lat/np.pi)*H
    b=b_lo+(b_hi-b_lo)*rng.random(n)**4
    s=s_lo+(s_hi-s_lo)*rng.random(n)
    t=rng.random(n)
    col=np.where(t[:,None]<0.25,[0.75,0.85,1.0],np.where(t[:,None]<0.85,[1.0,1.0,1.0],[1.0,0.9,0.75]))
    for uu,ww,la,br,sg,c in zip(u,w,lat,b,s,col):
        sx=sg/max(np.cos(la),0.05)          # etirement equirect compense
        rx=int(np.ceil(sx*3)); ry=int(np.ceil(sg*3))
        y0,y1=max(int(ww)-ry,0),min(int(ww)+ry+1,H)
        xs=np.arange(int(uu)-rx,int(uu)+rx+1); ys=np.arange(y0,y1)
        if len(ys)==0: continue
        gk=np.exp(-((xs[None,:]+0.5-uu)**2/(2*sx*sx)+(ys[:,None]+0.5-ww)**2/(2*sg*sg)))
        img[y0:y1][:, xs%W]+=gk[...,None]*br*c
add(9000,0.10,0.9,0.5,0.75)
add(900,0.5,1.6,0.65,0.95)
add(60,1.5,3.0,0.9,1.4)
out=np.clip(img,0,1)**(1/2.2)
Image.fromarray((out*255+0.5).astype(np.uint8)).save('out/SpaceStarfield_Dome.png',optimize=True)
