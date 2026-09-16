"""Generate a self-contained, Y-up, metre-scale GLB. Requires Pillow for preview/text."""
import json, math, struct
from array import array
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

OUT = Path(__file__).resolve().parent
materials = []
objects = []
def mat(name, color, metal=0, rough=.75, glow=False):
    m = {'name': name, 'pbrMetallicRoughness': {'baseColorFactor': [*color, 1], 'metallicFactor':metal,'roughnessFactor':rough}}
    if glow: m['emissiveFactor'] = [c*.6 for c in color]
    materials.append(m)
    return len(materials)-1
ground=mat('Basalt concrete',(.115,.15,.19))
steel=mat('Blue graphite steel',(.20,.28,.34),.6)
top=mat('Driving surface',(.32,.40,.44),.15)
dark=mat('Rubber and recesses',(.035,.055,.075))
white=mat('Ivory markings',(.82,.90,.88))
cyan=mat('Mobility cyan',(.04,.75,.86),.25,glow=True)
orange=mat('Safety amber',(1,.46,.075),.2)
red=mat('Target vermilion',(.95,.16,.10),.15)
green=mat('Climbing mint',(.26,.87,.55),.15)
purple=mat('Platforms violet',(.63,.43,.94),.2)

def mesh(name, verts, faces, material):
    objects.append((name,verts,faces,material))
def box(name, pos, size, material):
    x,y,z=pos; a,b,c=[s/2 for s in size]
    v=[(x-a,y-b,z-c),(x+a,y-b,z-c),(x+a,y+b,z-c),(x-a,y+b,z-c),
       (x-a,y-b,z+c),(x+a,y-b,z+c),(x+a,y+b,z+c),(x-a,y+b,z+c)]
    f=[(0,2,1),(0,3,2),(4,5,6),(4,6,7),(0,1,5),(0,5,4),
       (3,7,6),(3,6,2),(0,4,7),(0,7,3),(1,2,6),(1,6,5)]
    mesh(name,v,f,material)
def ramp(name,x,z,width,length,height,material):
    v=[(x-width/2,0,z),(x+width/2,0,z),(x-width/2,0,z+length),(x+width/2,0,z+length),
       (x-width/2,height,z+length),(x+width/2,height,z+length)]
    mesh(name,v,[(0,4,5),(0,5,1),(0,2,4),(1,5,3),(2,3,5),(2,5,4),(0,1,3),(0,3,2)],material)
def cylinder(name, center, radius, depth, material, axis='y', n=32):
    x,y,z=center; v=[]
    for d in [-depth/2,depth/2]:
        for i in range(n):
            a=2*math.pi*i/n; u=radius*math.cos(a); w=radius*math.sin(a)
            v.append((x+u,y+d,z+w) if axis=='y' else (x+u,y+w,z-d))
    # ring order viewed down +Y has negative normal; bottom is forward winding
    f=[]
    for i in range(1,n-1): f.extend([(0,i,i+1),(n,n+i+1,n+i)])
    for i in range(n):
        j=(i+1)%n; f.extend([(i,n+j,j),(i,n+i,n+j)])
    mesh(name,v,f,material)
def text_floor(text,x,z,size,material,height=.045):
    font=ImageFont.truetype('C:/Windows/Fonts/consolab.ttf',18)
    bounds=font.getbbox(text); im=Image.new('L',(bounds[2],bounds[3]-bounds[1]),0)
    ImageDraw.Draw(im).text((0,-bounds[1]),text,font=font,fill=255)
    s=size/im.height; verts=[]; faces=[]
    for j in range(im.height):
        i=0
        while i<im.width:
            if im.getpixel((i,j))<100: i+=1; continue
            start=i
            while i<im.width and im.getpixel((i,j))>=100: i+=1
            a=x+(start-im.width/2)*s; b=x+(i-im.width/2)*s
            c=z+(im.height/2-j)*s; d=c-s; k=len(verts)
            verts.extend([(2*x-a,height,2*z-c),(2*x-b,height,2*z-c),(2*x-b,height,2*z-d),(2*x-a,height,2*z-d)])
            faces.extend([(k,k+1,k+2),(k,k+2,k+3)])
    mesh('Marking_'+text,verts,faces,material)

box('Foundation_180x160m',(0,-1,0),(180,2,160),ground)
# Recessed zone pads keep every entry flush with the driving surface.
for name,x,z,w,d,col in [('SLOPES',-47,40,78,65,cyan),('TARGETS',47,40,72,65,orange),('CLIMB',-49,-42,74,58,green),('PLATFORMS',47,-42,72,58,purple)]:
    box(name+'_pad',(x,.006,z),(w,.012,d),dark)
    for sx in [-1,1]: box(name+'_border',(x+sx*w/2,.025,z),(.18,.025,d),col)
    for sz in [-1,1]: box(name+'_border',(x,.025,z+sz*d/2),(w,.025,.18),col)
    text_floor(name,x,z-d/2+3,2.2,col)
# Subtle concrete joints and cross-shaped circulation lanes.
for x in range(-80,81,10): box('Deck_joint',(x,.013,0),(.035,.01,160),steel)
for z in range(-70,71,10): box('Deck_joint',(0,.014,z),(180,.01,.035),steel)
for z in range(-70,76,6):
    if abs(z)>15: box('Main_road_dash',(0,.028,z),(.3,.02,2.6),white)
for x in range(-80,81,6):
    if abs(x)>16: box('Cross_road_dash',(x,.028,0),(2.6,.02,.3),white)
cylinder('Spawn_pad',(0,.045,0),10,.09,steel,n=64)
for i in range(32):
    a=2*math.pi*i/32
    box('Spawn_ring',(9.5*math.cos(a),.105,9.5*math.sin(a)),(.65,.03,.65),cyan)
text_floor('START',0,-3,2,white,.13)
box('Spawn_cross',(0,.11,1),(3,.04,.3),cyan)
box('Spawn_cross',(0,.11,1),(.3,.04,3),cyan)
# Four inclines: fixed 22 m horizontal run, full-width top landings.
for i,deg in enumerate([10,20,30,45]):
    x=-76+i*19; z=29; length=22; h=length*math.tan(math.radians(deg))
    ramp(f'Ramp_{deg}deg_collision',x,z,13,length,h,top)
    ramp(f'Ramp_{deg}deg_left_edge',x-6.3,z,.28,length,h+.045,cyan)
    ramp(f'Ramp_{deg}deg_right_edge',x+6.3,z,.28,length,h+.045,cyan)
    box(f'Ramp_{deg}deg_landing',(x,h-.5,z+length+5),(13,1,10),steel)
    for xx in [-5,5]: box('Landing_support',(x+xx,h/2,z+length+8),(1.2,h,1.2),steel)
    box('Landing_stop',(x,h+.4,z+length+10),(13,.8,.6),orange)
    text_floor(str(deg)+' DEG',x,24,1.5,cyan)
# Shooting range, three lanes, each with near/mid/far targets facing -Z.
box('Range_backstop',(48,8,72),(70,16,2),steel)
for x in [14,36,58,80]:
    box('Lane_marking',(x,.03,44),(.14,.03,46),orange)
for lane,x in enumerate([25,47,69]):
    for idx,z in enumerate([32,48,64]):
        tx=x+[-4,4,0][idx]; r=[2,2.7,3.5][idx]; y=r+2
        box(f'Target_L{lane+1}_{idx+1}_foot',(tx,.3,z),(4,.6,3),steel)
        box('Target_support',(tx,y/2,z),(.5,y,.5),steel)
        cylinder(f'Target_L{lane+1}_{idx+1}_back',(tx,y,z),r,.7,dark,'z')
        for j,(scale,col) in enumerate([(1,white),(.78,red),(.55,white),(.3,red),(.10,orange)]):
            cylinder(f'Target_L{lane+1}_{idx+1}_ring{j}',(tx,y,z-.37-j*.025),r*scale,.03,col,'z')
    text_floor('FIRE '+str(lane+1),x,18,1.4,orange)
# Broad continuous surfaces for insect-leg adhesion: straight wall, L-corner, overhang.
box('Climb_vertical_12m',(-77,6,-43),(2,12,32),top)
box('Climb_vertical_20m',(-58,10,-59),(29,20,2),top)
box('Climb_inside_corner',(-43.5,10,-49),(2,20,22),top)
box('Climb_overhang_wall',(-24,7,-51),(2,14,25),steel)
box('Climb_overhang_ceiling',(-29,14,-51),(12,1,25),top)
for y in [4,8,12,16,20]:
    box('Climb_height_band',(-58,y,-57.97),(28,.13,.04),green)
for y in [4,8,12]:
    box('Climb_height_band',(-75.97,y,-43),(.04,.13,31),green)
for x,z,h in [(-77,-43,12),(-58,-59,20),(-24,-51,14)]:
    box('Wall_top_marker',(x,h+.04,z),(2.15,.08,4),green)
text_floor('12M / 20M',-61,-20,1.4,green)
text_floor('OVERHANG',-27,-32,1.0,green)
# Wide staggered jump/hover platforms, with accessible beginner ramp.
for i,(x,z,h,w,d) in enumerate([(22,-28,2,14,12),(41,-29,4,14,12),(61,-30,7,15,14),(71,-52,11,16,15),(48,-59,15,16,15),(23,-58,9,14,14)]):
    box(f'Platform_{i+1}_top_{h}m',(x,h-.5,z),(w,1,d),top)
    box(f'Platform_{i+1}_edge',(x,h-.18,z-d/2-.02),(w,.27,.15),purple)
    box(f'Platform_{i+1}_edge',(x-w/2-.02,h-.18,z),(.15,.27,d),purple)
    for dx in [-w/2+1.2,w/2-1.2]:
        for dz in [-d/2+1.2,d/2-1.2]: box('Platform_column',(x+dx,(h-1)/2,z+dz),(.9,h-1,.9),steel)
    cylinder('Platform_landing_marker',(x,h+.02,z),2,.025,purple)
ramp('Beginner_platform_access',22,-44,10,10,2,top)
# Outer curb, luminous posts and corner pylons.
for x in [-89,89]: box('Perimeter_curb',(x,.5,0),(1,1,160),steel)
for z in [-79,79]: box('Perimeter_curb',(0,.5,z),(180,1,1),steel)
for x in range(-80,81,20):
    for z in [-78,78]:
        box('Perimeter_bollard',(x,1.5,z),(.7,3,.7),steel)
        box('Perimeter_lamp',(x,2.6,z),(.78,.5,.78),cyan)
for x in [-86,86]:
    for z in [-76,76]:
        box('Corner_pylon',(x,5,z),(2.4,10,2.4),steel)
        box('Corner_beacon',(x,10,z),(2.6,.5,2.6),orange)
text_floor('BLOCKFORGE // TEST FACILITY',0,-75,1.9,white)

def sub(a,b): return tuple(x-y for x,y in zip(a,b))
def cross(a,b): return (a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])
def dot(a,b): return sum(x*y for x,y in zip(a,b))
def norm(v):
    l=math.sqrt(dot(v,v)); return tuple(x/l for x in v) if l else (0,1,0)

def export():
    data=bytearray(); views=[]; access=[]; meshes=[]; nodes=[]
    def accessor(values,typ):
        while len(data)%4: data.append(0)
        start=len(data)
        for v in values: data.extend(struct.pack('<'+'f'*len(v),*v))
        views.append({'buffer':0,'byteOffset':start,'byteLength':len(data)-start,'target':34962})
        access.append({'bufferView':len(views)-1,'componentType':5126,'count':len(values),'type':typ,
                       'min':[min(v[i] for v in values) for i in range(len(values[0]))],
                       'max':[max(v[i] for v in values) for i in range(len(values[0]))]})
        return len(access)-1
    for name,v,f,m in objects:
        positions=[]; normals=[]
        vertex_normals=None
        if name.startswith('BG_Terrain_'):
            sums=[[0.,0.,0.] for _ in v]
            for tri in f:
                a,b,c=[v[i] for i in tri]
                n=cross(sub(b,a),sub(c,a))
                for idx in tri:
                    for axis in range(3): sums[idx][axis]+=n[axis]
            vertex_normals=[norm(n) for n in sums]
        for tri in f:
            a,b,c=[v[i] for i in tri]; n=norm(cross(sub(b,a),sub(c,a)))
            positions.extend([a,b,c]); normals.extend([vertex_normals[i] for i in tri] if vertex_normals else [n]*3)
        p=accessor(positions,'VEC3'); n=accessor(normals,'VEC3')
        attributes={'POSITION':p,'NORMAL':n}
        colors=globals().get('vertex_colors',{}).get(name)
        if colors:
            attributes['COLOR_0']=accessor([colors[i] for tri in f for i in tri],'VEC4')
        meshes.append({'name':name,'primitives':[{'attributes':attributes,'material':m}]})
        nodes.append({'name':name,'mesh':len(meshes)-1})
    nodes.append({'name':'SpawnPoint','translation':[0,.2,0],'extras':{'purpose':'Player robot spawn; forward +Z'}})
    doc={'asset':{'version':'2.0','generator':'Blockforge procedural training arena'},'scene':0,
         'scenes':[{'name':'Blockforge Training Arena','nodes':list(range(len(nodes)))}],
         'nodes':nodes,'meshes':meshes,'materials':materials,'accessors':access,'bufferViews':views,
         'buffers':[{'byteLength':len(data)}]}
    j=json.dumps(doc,separators=(',',':')).encode()
    j+=b' '*((-len(j))%4); data+=b'\0'*((-len(data))%4)
    glb=struct.pack('<III',0x46546c67,2,12+8+len(j)+8+len(data))+struct.pack('<II',len(j),0x4e4f534a)+j+struct.pack('<II',len(data),0x004e4942)+data
    (OUT/'Blockforge_Training_Arena.glb').write_bytes(glb)
    print(f'GLB: {len(objects)} meshes, {sum(len(o[2]) for o in objects)} triangles, {len(glb):,} bytes')

def preview(title='BLOCKFORGE  /  TRAINING ARENA', subtitle='180 x 160 m     |     4 zones     |     GLB / Y-up / metres', footer='01 SLOPES     02 TARGETS     03 CLIMB + OVERHANG     04 PLATFORMS', view_scale=8.1, focus=(0,0,0), filename='Arena_Preview.png'):
    W,H=2200,1700; im=Image.new('RGB',(W,H),(18,25,35)); draw=ImageDraw.Draw(im)
    eye=(160,210,-230); forward=norm(tuple(-v for v in eye)); right=norm(cross(forward,(0,1,0))); up=cross(right,forward)
    scale=view_scale; tris=[]; light=norm((-.4,1,-.5))
    def project(v):
        p=sub(v,focus)
        return (W/2+dot(p,right)*scale,H/2+70-dot(p,up)*scale)
    for name,v,faces,m in objects:
        color=materials[m]['pbrMetallicRoughness']['baseColorFactor'][:3]
        colors=globals().get('vertex_colors',{}).get(name)
        for face in faces:
            a,b,c=[v[i] for i in face]; normal=norm(cross(sub(b,a),sub(c,a)))
            if dot(normal,forward)>=0: continue
            shade=.50+.50*max(0,dot(normal,light))
            facecolor=[color[j]*sum(colors[i][j] for i in face)/3 for j in range(3)] if colors else color
            rgb=tuple(min(255,int(255*q*shade)) for q in facecolor)
            tris.append(([dot(t,forward) for t in [a,b,c]],[project(t) for t in [a,b,c]],rgb))
    # Per-pixel depth is essential for the large ground faces beneath obstacles.
    depth=array('f',[float('inf')])*(W*H); pixels=im.load()
    for ds,pts,color in tris:
        (ax,ay),(bx,by),(cx,cy)=pts
        den=(by-cy)*(ax-cx)+(cx-bx)*(ay-cy)
        if abs(den)<1e-8: continue
        for py in range(max(0,int(min(ay,by,cy))),min(H,int(max(ay,by,cy))+1)):
            for px in range(max(0,int(min(ax,bx,cx))),min(W,int(max(ax,bx,cx))+1)):
                u=((by-cy)*(px+.5-cx)+(cx-bx)*(py+.5-cy))/den
                v=((cy-ay)*(px+.5-cx)+(ax-cx)*(py+.5-cy))/den
                w=1-u-v
                if min(u,v,w)<-1e-7: continue
                d=u*ds[0]+v*ds[1]+w*ds[2]; idx=py*W+px
                if d<depth[idx]: depth[idx]=d; pixels[px,py]=color
    font=ImageFont.truetype('C:/Windows/Fonts/consolab.ttf',48)
    small=ImageFont.truetype('C:/Windows/Fonts/consola.ttf',25)
    draw.text((80,55),title,font=font,fill=(219,234,239))
    draw.text((82,122),subtitle,font=small,fill=(124,160,179))
    draw.text((82,H-85),footer,font=small,fill=(140,178,197))
    im.save(OUT/filename)

if __name__=='__main__':
    export(); preview()
