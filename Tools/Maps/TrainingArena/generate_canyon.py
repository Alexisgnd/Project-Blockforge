"""Integrated training level: abandoned canyon refinery, interconnected routes."""
import math, random
from pathlib import Path
import generate_arena as g

g.objects.clear()
g.OUT=Path(__file__).resolve().parent/'Canyon'
g.OUT.mkdir(exist_ok=True)
rng=random.Random(402)
box,mesh,cyl,mat=g.box,g.mesh,g.cylinder,g.mat
sand=mat('Warm canyon dust',(.43,.30,.20))
rock=mat('Red sandstone',(.48,.245,.145))
rocklight=mat('Sunlit sandstone',(.63,.36,.21))
rockdark=mat('Deep sandstone seams',(.29,.145,.095))
concrete=mat('Weathered concrete',(.43,.46,.43))
roadmat=mat('Dark road composite',(.15,.19,.20))
metal=mat('Oxidized refinery blue',(.18,.31,.34),.45)
trim=mat('Pale painted panels',(.72,.73,.62),.25)
rust=mat('Industrial ochre',(.73,.33,.11),.4)
black=g.dark; amber=g.orange; glow=g.cyan

def prism(name,cx,cz,rings,n,m,phase=0,rough=.12):
    factors=[rng.uniform(1-rough,1+rough) for _ in range(n)]
    v=[]
    for radius,y in rings:
        for i in range(n):
            a=2*math.pi*i/n+phase
            v.append((cx+math.cos(a)*radius*factors[i],y,cz+math.sin(a)*radius*factors[i]))
    f=[]
    for k in range(len(rings)-1):
        for i in range(n):
            j=(i+1)%n; a=k*n+i; b=k*n+j; c=(k+1)*n+j; d=(k+1)*n+i
            f.extend([(a,d,c),(a,c,b)])
    for i in range(1,n-1):
        f.extend([(0,i,i+1),((len(rings)-1)*n,(len(rings)-1)*n+i+1,(len(rings)-1)*n+i)])
    mesh(name,v,f,m)

def beam(name,a,b,width,thickness,m):
    # Watertight traversable slab following any horizontal heading and grade.
    dx=b[0]-a[0]; dz=b[2]-a[2]; L=math.hypot(dx,dz)
    nx=-dz/L*width/2; nz=dx/L*width/2
    v=[(a[0]+nx,a[1]-thickness,a[2]+nz),(a[0]-nx,a[1]-thickness,a[2]-nz),
       (b[0]-nx,b[1]-thickness,b[2]-nz),(b[0]+nx,b[1]-thickness,b[2]+nz),
       (a[0]+nx,a[1],a[2]+nz),(a[0]-nx,a[1],a[2]-nz),
       (b[0]-nx,b[1],b[2]-nz),(b[0]+nx,b[1],b[2]+nz)]
    f=[(0,1,2),(0,2,3),(4,6,5),(4,7,6),(0,4,5),(0,5,1),(1,5,6),(1,6,2),(2,6,7),(2,7,3),(3,7,4),(3,4,0)]
    mesh(name,v,f,m)

def route(name,points,width=16,rails=False):
    points=[(x,y+.035,z) for x,y,z in points]
    for i,(a,b) in enumerate(zip(points,points[1:])):
        beam(name+f'_surface_{i}',a,b,width,.65 if rails else .16,roadmat)
        dx,dz=b[0]-a[0],b[2]-a[2]; L=math.hypot(dx,dz); nx=-dz/L; nz=dx/L
        for side in [-1,1]:
            aa=(a[0]+nx*side*(width/2-.45),a[1]+.025,a[2]+nz*side*(width/2-.45))
            bb=(b[0]+nx*side*(width/2-.45),b[1]+.025,b[2]+nz*side*(width/2-.45))
            beam(name+'_edge_paint',aa,bb,.22,.02,amber)
            if rails:
                # Interrupted low barriers leave room to jump and climb onto the route.
                for k in range(int(L/8)):
                    t=(k+.5)*8/L; t2=min(1,t+3/L)
                    p=tuple(aa[j]+(bb[j]-aa[j])*t for j in range(3))
                    q=tuple(aa[j]+(bb[j]-aa[j])*t2 for j in range(3))
                    beam(name+'_guard',(p[0],p[1]+.8,p[2]),(q[0],q[1]+.8,q[2]),.45,.8,concrete)
        if rails:
            for t in [.15,.8]:
                p=tuple(a[j]+(b[j]-a[j])*t for j in range(3))
                if p[1]>2: box(name+'_pier',(p[0],p[1]/2-.4,p[2]),(2.5,p[1]-.8,3),concrete)

def building(name,x,z,w,d,h):
    box(name+'_climbable_facade',(x,(h-.6)/2,z),(w,h-.6,d),concrete)
    box(name+'_roof',(x,h-.3,z),(w+1.4,.6,d+1.4),metal)
    for xx in [-w/2+.6,w/2-.6]: box(name+'_corner',(x+xx,h/2,z-d/2-.12),(1,h,.25),metal)
    for i in range(max(1,int(w/5))):
        xx=x-w/2+2.5+i*5
        box(name+'_window_recess',(xx,h*.62,z-d/2-.14),(3,1.7,.18),black)
        box(name+'_window',(xx,h*.62,z-d/2-.25),(2.5,.22,.04),glow)
    box(name+'_door',(x,2.8,z-d/2-.15),(5,5.6,.2),metal)
    for y in [1,2,3,4,5]: box(name+'_door_rib',(x,y,z-d/2-.28),(4.7,.08,.07),trim)

def target(name,x,y,z,angle=0):
    start=len(g.objects)
    box(name+'_base',(0,.3,0),(3,.6,2.5),metal)
    box(name+'_post',(0,1.7,0),(.35,2.8,.35),rust)
    for i,(r,m) in enumerate([(1.65,black),(1.4,trim),(1.05,g.red),(.67,trim),(.3,amber)]):
        cyl(name+'_disc_'+str(i),(0,3,-i*.055),r,.07,m,'z',24)
    c,s=math.cos(angle),math.sin(angle)
    for i in range(start,len(g.objects)):
        name0,v,f,m=g.objects[i]
        g.objects[i]=(name0,[(x+a*c+b*s,y+h,z-a*s+b*c) for a,h,b in v],f,m)

def crate(name,x,y,z,w=7):
    box(name,(x,y+2.5,z),(w,5,5),rust)
    for dx in [-w/2+.2,w/2-.2]: box(name+'_frame',(x+dx,y+2.5,z),(.3,5.2,5.2),metal)
    for xx in range(int(w)):
        box(name+'_rib',(x-w/2+xx+.5,y+2.5,z-2.55),(.13,4.5,.1),trim)

# Organic terrain base with stratified canyon edges, no square testing board.
n=64; outline=[]
for i in range(n):
    a=i*2*math.pi/n; f=rng.uniform(.96,1.04)
    outline.append((119*math.cos(a)*f,0,96*math.sin(a)*f))
v=[(0,0,0)]+outline
mesh('Canyon_floor',v,[(0,1+(i+1)%n,1+i) for i in range(n)],sand)
for i in range(n):
    a=outline[i]; b=outline[(i+1)%n]
    mesh('Bedrock_edge', [a,b,(b[0],-7,b[2]),(a[0],-7,a[2])],[(0,1,2),(0,2,3)],rockdark)
for i in range(31):
    a=2*math.pi*i/31; x=109*math.cos(a); z=86*math.sin(a)
    h=rng.uniform(18,31) if z>0 else rng.uniform(5,12)
    r=rng.uniform(11,18)
    prism('Canyon_cliff',x,z,[(r,0),(r*.94,h*.45),(r*.79,h*.52),(r*.71,h),(r*.5,h+1)],7,rock if i%3 else rocklight,rough=.22)
    prism('Cliff_sedimentary_band',x,z,[(r*.96,h*.42),(r*.93,h*.49)],7,rockdark,rough=.08)
# Scenic mesa under the western route, continuous climbable industrial retaining face.
prism('West_mesa',-67,27,[(31,0),(28,6),(25,9.8)],9,rocklight,rough=.08)
box('Mesa_retaining_climb_wall',(-65,4.65,12),(40,9.3,2),concrete)
box('Mesa_deck',(-64,9.65,27),(40,.7,30),metal)
route('West_switchback',[(-78,.16,-52),(-75,.16,-28),(-65,10,12),(-65,10,28)],17)
route('Mesa_to_reactor_bridge',[(-50,10,28),(-20,10,28),(0,10,28)],15,True)
route('Mesa_rear_descent',[(-63,10,41),(-57,10,55),(-27,.16,66),(3,.16,66)],16,True)
# Main looping roadway, enough clearance for a large assembled robot.
route('Valley_loop',[(-79,.15,-51),(-42,.15,-67),(5,.15,-66),(48,.15,-56),(83,.15,-34),(89,.15,13),(79,.15,51),(47,.15,65),(3,.15,66)],17)
route('Under_bridge_shortcut',[(-42,.16,-61),(-30,.16,-18),(-31,.16,25),(-25,.16,60)],15)
# Central facility forms several climbable faces and a drive-through underpass.
box('Reactor_left_abutment',(-13,4.6,27),(5,9.2,29),concrete)
box('Reactor_right_abutment',(13,4.6,27),(5,9.2,29),concrete)
box('Reactor_elevated_plaza',(0,9.6,27),(31,.8,31),metal)
cyl('Reactor_octagonal_foot',(0,10.4,31),9,.8,concrete,n=8)
cyl('Reactor_chamber',(0,17,31),6.5,13,metal,n=12)
for y in [11,15,20,23]: cyl('Reactor_armour_ring',(0,y,31),7,.6,trim,n=12)
for i in range(8):
    a=2*math.pi*i/8
    box('Reactor_luminous_core',(6.55*math.cos(a),17,31+6.55*math.sin(a)),(.55,9,.55),glow)
cyl('Reactor_cap',(0,24,31),8,1.5,metal,n=12)
cyl('Reactor_spire',(0,29,31),1,9,rust,n=8)
route('Reactor_front_access',[(0,.16,-20),(0,10,12)],17)
# Eastern rooftop route, freight yard and bridge back into the same reactor hub.
building('Refinery',60,13,33,34,14)
route('Refinery_roof_access',[(63,.16,-49),(63,14,-4),(63,14,13)],17)
route('Skybridge',[(44,14,13),(16,10,17)],13,True)
building('Pump_house',42,52,20,16,8)
route('Pump_roof_link',[(59,14,30),(53,8,45),(43,8,52)],12,True)
route('Pump_descent',[(33,8,52),(14,.16,61)],12,True)
for x,z in [(77,10),(77,26)]:
    cyl('Refinery_tank',(x,8,z),4,16,trim,n=16)
    for y in [1,8,15]: cyl('Tank_hoop',(x,y,z),4.2,.35,metal,n=16)
# Platforms are broken freight viaduct segments crossing the courtyard.
for i,(x,z,h) in enumerate([(15,-48,2),(29,-34,4.5),(31,-14,7),(31,6,10)]):
    box('Broken_viaduct_deck_'+str(i),(x,h-.5,z),(12,1,12),concrete)
    for dx in [-4,4]: box('Viaduct_pier',(x+dx,(h-1)/2,z),(1.5,h-1,7),metal)
    box('Viaduct_hazard_edge',(x,h-.2,z-6.05),(12,.35,.1),amber)
route('Broken_viaduct_entry',[(14,.16,-65),(15,2,-54)],11)
# Smaller routes, natural banks, cover, climbable slab with a usable ceiling.
building('Service_hangar',-53,-24,24,20,9)
box('Hangar_overhang',(-53,8.65,-39),(27,.7,11),metal)
for x in [-64,-42]: box('Hangar_canopy_post',(x,4.2,-43),(1,8.4,1),rust)
route('Hangar_roof_bank',[(-22,.15,-50),(-41,9,-24)],13)
route('Hangar_roof_exit',[(-53,9,-14),(-64,10,12)],12,True)
for x,z,w in [(-13,-45,8),(45,-48,7),(-86,7,9),(26,41,8),(-44,46,7)]: crate('Freight_container',x,.1,z,w)
for x,z,r in [(-17,-8,5),(49,-7,4),(-87,-29,5),(18,73,5),(91,-57,7),(-83,56,6)]:
    prism('Rock_outcrop',x,z,[(r,0),(r*.8,3),(r*.3,5)],6,rocklight)
# Cables/pipes tie the industrial structures visually together.
for z in [18,21]:
    beam('Utility_pipe_overpass',(-65,13,z),(-17,13,z),.65,.65,rust)
for x in [-48,-22]:
    box('Pipe_support',(x,6.5,19.5),(1,13,1),metal)
    box('Pipe_crossbar',(x,13,19.5),(2,.5,6),metal)
# Targets are encounters embedded along ground routes, rooftops and climb exits.
for i,(x,y,z,a) in enumerate([(-57,10,34,0),(-46,0,7,.3),(-12,0,44,0),(7,10,17,0),(53,14,22,-.5),(38,8,53,0),(28,7,-12,0),(-60,9,-20,0),(76,0,48,-.5),(4,0,70,0),(-17,0,-40,.4),(48,0,-36,-.4)]):
    target('Target_'+str(i+1).zfill(2),x,y,z,a)
# Spawn is a courtyard on the main circuit, with sight lines into the environment.
cyl('Spawn_courtyard',(-19,.12,-66),11,.24,concrete,n=32)
g.text_floor('B-07',-19,-68,3,trim,.255)
for x in [-28,-10]:
    box('Spawn_beacon',(x,1.1,-74),(.5,2.2,.5),metal)
    box('Spawn_light',(x,2.2,-74),(.7,.4,.7),glow)
g.text_floor('NORTH RIDGE',-65,25,1.5,trim,10.075)
g.text_floor('02',62,8,3,trim,14.075)
# Diegetic lighting masts and cargo details, sparse enough to keep driving room.
for x,z in [(-82,-50),(-37,-64),(80,-31),(87,43),(-52,59),(16,66)]:
    box('Light_mast',(x,5,z),(.45,10,.45),metal)
    box('Light_head',(x,10,z),(3,.5,1),trim)
    box('Light_emitter',(x,9.72,z),(2.5,.06,.65),glow)

# Continuous exterior plain, with broad horizon hills only near the map limits.
background_start=len(g.objects)
from detailed_terrain import build as build_background
build_background(g)

if __name__=='__main__':
    g.export()
    p=g.OUT/'Blockforge_Training_Arena.glb'
    # Set the actual player spawn and scene name in the GLB JSON chunk.
    import json,struct
    b=p.read_bytes(); jl=struct.unpack_from('<I',b,12)[0]; doc=json.loads(b[20:20+jl])
    doc['scenes'][0]['name']='Blockforge - Red Canyon Proving Grounds'
    doc['nodes'][-1]['translation']=[-19,.3,-66]
    background_nodes=list(range(background_start,len(g.objects)))
    root=len(doc['nodes'])
    doc['nodes'].append({'name':'BACKGROUND_NON_PLAYABLE','children':background_nodes,
                         'extras':{'nonPlayable':True,'collisionRequired':False,'purpose':'Exterior scenery beyond canyon perimeter'}})
    doc['scenes'][0]['nodes']=list(range(background_start))+[len(g.objects),root]
    doc['scenes'][0]['extras']={'playableArea':{'shape':'ellipse','radiusX':100,'radiusZ':80},
                              'integrationNote':'Enforce playable bounds in game for climbing or flying robots; exterior scenery is not a gameplay area.'}
    j=json.dumps(doc,separators=(',',':')).encode(); j+=b' '*((-len(j))%4)
    tail=b[20+jl:]; blob=struct.pack('<III',0x46546c67,2,20+len(j)+len(tail))+struct.pack('<II',len(j),0x4e4f534a)+j+tail
    p.write_bytes(blob)
    g.preview('BLOCKFORGE / RED CANYON','Continuous desert plain  |  Approx. 1.36 x 1.16 km  |  GLB',
              'SCULPTED WASHES / EROSION / ROCK DEPOSITS / DESERT SCRUB',1.43)
    g.preview('RED CANYON / TERRAIN DETAIL','Geometric erosion, sediment banks, fractured rock and dry scrub',
              'DETAIL VIEW / EASTERN PLAIN',4.2,focus=(260,15,55),filename='Terrain_Detail.png')
