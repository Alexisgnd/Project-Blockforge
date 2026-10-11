# =========================================================
# PATCH DU VAISSEAU ShipGarage_V2.glb (vitres d'observation)
# Usage : python patch_ship_glb.py <glb d'origine> <glb de sortie>
# A lancer sur le GLB D'ORIGINE (git show <commit>:Assets/_Project/Art/Models/ShipGarage_V2.glb)
# - "Side internal frame" (pave plein de 2 m derriere les vitres) remplace par
#   un cadre perce de 6 ouvertures par cote, alignees sur les vitres ;
# - les 12 "Observation glass" recoivent un materiau verre (alpha BLEND,
#   teinte cyan, alpha 0.12) au lieu du materiau opaque des ecrans.
# Dependances : numpy
# =========================================================
import json,struct,sys,numpy as np
src,dst=sys.argv[1],sys.argv[2]
b=open(src,'rb').read()
jl=struct.unpack('<I',b[12:16])[0]; g=json.loads(b[20:20+jl])
off=20+jl; bl=struct.unpack('<I',b[off:off+4])[0]; binb=bytearray(b[off+8:off+8+bl])
assert len(g['buffers'])==1
def acc_bounds(i): a=g['accessors'][i]; return np.array(a['min']),np.array(a['max'])
def add_accessor(arr,target=34962):
    global binb
    while len(binb)%4: binb.append(0)
    data=arr.astype('<f4').tobytes(); bo=len(binb); binb+=data
    g['bufferViews'].append({'buffer':0,'byteOffset':bo,'byteLength':len(data),'target':target})
    acc={'bufferView':len(g['bufferViews'])-1,'componentType':5126,'count':len(arr),'type':'VEC3'}
    acc['min']=arr.min(0).tolist(); acc['max']=arr.max(0).tolist()
    g['accessors'].append(acc); return len(g['accessors'])-1
def box(x0,x1,y0,y1,z0,z1):
    P=[];N=[]
    c=[(x,y,z) for x in (x0,x1) for y in (y0,y1) for z in (z0,z1)]
    def quad(a,bq,cq,d,n):  # a,b,c,d counter-clockwise seen from outside (normal n)
        for t in ((a,bq,cq),(a,cq,d)):
            P.extend(t); N.extend([n]*3)
    v=lambda i,j,k:(x1 if i else x0, y1 if j else y0, z1 if k else z0)
    quad(v(1,0,0),v(1,1,0),v(1,1,1),v(1,0,1),(1,0,0))
    quad(v(0,0,1),v(0,1,1),v(0,1,0),v(0,0,0),(-1,0,0))
    quad(v(0,1,0),v(0,1,1),v(1,1,1),v(1,1,0),(0,1,0))
    quad(v(0,0,1),v(0,0,0),v(1,0,0),v(1,0,1),(0,-1,0))
    quad(v(0,0,1),v(1,0,1),v(1,1,1),v(0,1,1),(0,0,1))
    quad(v(1,0,0),v(0,0,0),v(0,1,0),v(1,1,0),(0,0,-1))
    return P,N
names={i:n.get('name','') for i,n in enumerate(g['nodes'])}
frames=[i for i,n in names.items() if n=='Side internal frame']
glass=[i for i,n in names.items() if n=='Observation glass']
assert len(frames)==2 and len(glass)==12
# nouveau materiau verre
g['materials'].append({'name':'Glass • observation','alphaMode':'BLEND','doubleSided':True,
    'pbrMetallicRoughness':{'baseColorFactor':[0.42,0.72,0.88,0.12],'metallicFactor':0.0,'roughnessFactor':0.04}})
glass_mat=len(g['materials'])-1
for ni in glass:
    for p in g['meshes'][g['nodes'][ni]['mesh']]['primitives']: p['material']=glass_mat
for ni in frames:
    node=g['nodes'][ni]; assert 'translation' not in node and 'matrix' not in node
    mesh=g['meshes'][node['mesh']]; prim=mesh['primitives'][0]
    mn,mx=acc_bounds(prim['attributes']['POSITION'])
    side=1 if mn[0]>0 else -1
    # ouvertures = etendue des vitres de ce cote
    opens=[]
    for gi in glass:
        gmn,gmx=acc_bounds(g['meshes'][g['nodes'][gi]['mesh']]['primitives'][0]['attributes']['POSITION'])
        if np.sign(gmn[0])==side: opens.append((gmn[1],gmx[1],gmn[2],gmx[2]))
    opens.sort(key=lambda o:o[2]); assert len(opens)==6
    y0,y1=opens[0][0],opens[0][1]
    x0,x1=mn[0],mx[0]
    P=[];N=[]
    for bx in [(x0,x1,mn[1],y0,mn[2],mx[2]),(x0,x1,y1,mx[1],mn[2],mx[2])]:
        p,n=box(*bx);P+=p;N+=n
    zs=[mn[2]]+[v for o in opens for v in (o[2],o[3])]+[mx[2]]
    for k in range(0,len(zs),2):
        if zs[k+1]-zs[k]>1e-4:
            p,n=box(x0,x1,y0,y1,zs[k],zs[k+1]);P+=p;N+=n
    prim['attributes']={'POSITION':add_accessor(np.array(P)),'NORMAL':add_accessor(np.array(N))}
    prim.pop('indices',None)
    print('frame',ni,'side',side,'x',x0,x1,'openings y',y0,y1,'z',[ (round(o[2],2),round(o[3],2)) for o in opens],'tris',len(P)//3)
g['buffers'][0]['byteLength']=len(binb)
while len(binb)%4: binb.append(0)
j=json.dumps(g,separators=(',',':'),ensure_ascii=False).encode('utf-8')
while len(j)%4: j+=b' '
out=struct.pack('<III',0x46546C67,2,12+8+len(j)+8+len(binb))+struct.pack('<II',len(j),0x4E4F534A)+j+struct.pack('<II',len(binb),0x004E4942)+bytes(binb)
open(dst,'wb').write(out); print('written',len(out))
