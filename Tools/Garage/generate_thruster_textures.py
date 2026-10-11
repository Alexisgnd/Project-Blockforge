# =========================================================
# TEXTURES DES PROPULSEURS DU GARAGE
# Usage : python generate_thruster_textures.py -> out/Thruster{Plume,Core,Spark}.png
# Plume/Core : plaquees sur des plans croises (ShipThrusterFX) ;
# U = travers du plan (bords qui s'estompent), V = longueur (0 = tuyere) ;
# couleur deja ponderee par l'alpha (materiau additif). Graines fixes.
# Utilisees par Blockforge > Setup Garage Thrusters.
# Dependances : numpy, pillow
# =========================================================
import numpy as np
from PIL import Image
import os
os.makedirs('out',exist_ok=True)
rng=np.random.default_rng(42)
# Plans croises : U = travers du plan (0..1, bords qui s'estompent), V = longueur (0 = tuyere)
W,H=128,256
u=np.linspace(0,1,W)[None,:]; v=np.linspace(0,1,H)[:,None]
x=np.abs(u*2-1)                                   # 0 au centre, 1 au bord
def streaks(n_terms, seed):
    r=np.random.default_rng(seed); s=np.zeros((H,W))
    for k,a in n_terms:
        ph=r.random()*6.283; drift=r.uniform(-3,3)
        s+=a*np.sin(6.283*k*u+ph+v*drift)
    return 0.8+0.2*s/np.abs(s).max()
def plume(len_pow, edge_pow, diamonds, c_near, c_mid, c_far, seed):
    fall=np.clip(1-v,0,1)**len_pow*(1-diamonds+diamonds*np.cos(v*6.283*3.5)**2)
    fall*=np.clip(v/0.03,0,1)**0.5
    edge=np.clip(1-x,0,1)**edge_pow
    a=np.clip(fall*edge*streaks(((3,0.4),(7,0.3),(13,0.2),(29,0.1)),seed),0,1)
    t=v[...,None]
    col=np.where(t<0.3,c_near+(c_mid-c_near)*(t/0.3),c_mid+(c_far-c_mid)*((t-0.3)/0.7))
    col=col+(np.array([1,1,1])-col)*(np.clip(1-x,0,1)**6)[...,None]*0.6   # coeur blanc au centre
    return np.dstack([np.clip(col,0,1)*a[...,None],a])
p=plume(1.5,1.6,0.18,np.array([0.8,1.0,1.0]),np.array([0.15,0.75,1.0]),np.array([0.05,0.25,0.9]),1)
c=plume(2.6,2.4,0.0,np.array([1.0,1.0,1.0]),np.array([0.7,0.97,1.0]),np.array([0.3,0.8,1.0]),2)
# Unity lit V depuis le BAS de l'image : on retourne pour avoir V = 0 (tuyere) en bas
Image.fromarray((np.flipud(p)*255+0.5).astype(np.uint8),'RGBA').save('out/ThrusterPlume.png')
Image.fromarray((np.flipud(c)*255+0.5).astype(np.uint8),'RGBA').save('out/ThrusterCore.png')
# Etincelle : point doux
S=64; yy,xx=np.mgrid[0:S,0:S]+0.5; r=np.hypot(xx-S/2,yy-S/2)/(S/2)
d=np.clip(1-r,0,1)**2.5
Image.fromarray((np.clip(np.dstack([d*0.6,d*0.95,d,d]),0,1)*255+0.5).astype(np.uint8),'RGBA').save('out/ThrusterSpark.png')
