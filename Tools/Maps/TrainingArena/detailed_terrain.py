"""Sculpted exterior: geometric erosion, dry washes, rocks and desert plants."""
import math, random

def build(g):
    rng=random.Random(98713)
    segments,rings=336,144
    colors=[]; vertices=[]; faces=[]
    rockm=g.mat('Exterior fractured sandstone',(.47,.29,.17))
    lightrock=g.mat('Exterior pale sediment',(.58,.43,.28))
    shale=g.mat('Dark shale fragments',(.30,.255,.20))
    grass=g.mat('Dry ochre scrub',(.40,.365,.205))
    wood=g.mat('Dead branches',(.255,.20,.145))
    terrainmat=g.mat('Desert mineral surface - vertex colors',(1,1,1),rough=1)
    def smooth(t):
        t=max(0,min(1,t)); return t*t*(3-2*t)
    def field(x,z):
        a=math.atan2(z/580,x/680)
        outline=1+.022*math.sin(5*a)+.016*math.sin(9*a+.8)
        t=(math.hypot(x/680,z/580)/outline-.14)/.86
        fade=smooth((t-.08)/.15)
        y=-.30+fade*(1.5*math.sin(x/73)*math.sin(z/91)+.9*math.sin((x+z)/109))
        onset=.55+.05*math.sin(3*a+.5)+.015*math.sin(7*a)
        crest=.83+.04*math.sin(4*a)
        height=117+28*math.sin(3*a+.4)+19*math.sin(7*a+1.3)+12*math.sin(11*a)
        h=0
        if t>onset:
            h=smooth((t-onset)/(crest-onset)) if t<=crest else 1-.86*smooth((t-crest)/(1-crest))
            y+=height*h
            gullies=(.5+.5*math.sin(a*31+2*math.sin(t*11+a*4)))**10
            y-=16*gullies*math.sin(math.pi*max(0,min(1,(t-onset)/(1-onset))))**2
            y+=4*math.sin(a*47+t*14)*h
        c1=-238+32*math.sin(z/90)+12*math.sin(z/38)
        c2=257+43*math.sin(z/115+.8)+14*math.sin(z/47)
        width=10+3*math.sin(z/71)
        bed=max(math.exp(-((x-c1)/width)**2),math.exp(-((x-c2)/(width+3))**2))
        bed*=1-smooth((abs(z)-345)/95)
        y-=6.5*bed*fade
        y+=fade*(.38*math.sin(x*.17+z*.07)*math.sin(z*.083)+.19*math.sin(x*.31-z*.11))
        variation=.026*math.sin(x/37+math.sin(z/43))+.016*math.sin(z/17+x/51)
        strata=(.5+.5*math.sin(y*.49+math.sin(a*13)))**5*h
        base=(.45+variation,.315+variation*.8,.205+variation*.6)
        tint=min(.65,bed*.52)
        base=tuple(base[i]*(1-tint)+(.56,.455,.32)[i]*tint for i in range(3))
        base=tuple(base[i]*(1-strata*.27)+(.64,.47,.30)[i]*strata*.27 for i in range(3))
        return y,(*base,1),t
    for ring in range(rings+1):
        t=ring/rings
        for i in range(segments):
            a=2*math.pi*i/segments
            outline=1+.022*math.sin(5*a)+.016*math.sin(9*a+.8)
            s=(.14+.86*t)*outline
            x,z=680*s*math.cos(a),580*s*math.sin(a)
            y,col,_=field(x,z)
            vertices.append((x,y,z)); colors.append(col)
    for ring in range(rings):
        for i in range(segments):
            j=(i+1)%segments; a=ring*segments+i; b=ring*segments+j
            c=(ring+1)*segments+j; d=(ring+1)*segments+i
            faces.extend([(a,b,c),(a,c,d)])
    name='BG_Terrain_Sculpted_plain_washes_and_eroded_hills'
    g.mesh(name,vertices,faces,terrainmat)
    g.vertex_colors={name:colors}
    skirt=[]; sf=[]
    for i in range(segments):
        x,y,z=vertices[rings*segments+i]; skirt.extend([(x,y,z),(x,-40,z)])
    for i in range(segments):
        j=(i+1)%segments; sf.extend([(2*i,2*i+1,2*j+1),(2*i,2*j+1,2*j)])
    g.mesh('BG_Outer_hidden_skirt',skirt,sf,rockm)

    # Batched details keep the mesh count low despite thousands of rock fragments.
    batches={}
    def add(name,v,f,m):
        verts,tris=batches.setdefault((name,m),([],[])); n=len(verts)
        verts.extend(v); tris.extend([tuple(n+i for i in face) for face in f])
    def stone(x,z,r,height,m):
        y,_,t=field(x,z)
        if t<.11 or t>.95:return
        n=7; rotation=rng.uniform(0,6.28); v=[]
        uneven=[rng.uniform(.72,1.20) for _ in range(n)]
        for level,scale in [(-.2,1),(.35,1.08),(1,.53)]:
            for i in range(n):
                a=rotation+2*math.pi*i/n
                vx=x+r*scale*uneven[i]*math.cos(a)
                vz=z+r*scale*uneven[i]*math.sin(a)*.7
                v.append((vx,field(vx,vz)[0]+height*level,vz))
        f=[]
        for k in range(2):
            for i in range(n):
                j=(i+1)%n; a=k*n+i;b=k*n+j;c=b+n;d=a+n
                f.extend([(a,d,c),(a,c,b)])
        for i in range(1,n-1):f.append((2*n,2*n+i+1,2*n+i))
        add('BG_Fractured_rock_and_scree',v,f,m)
    for cluster in range(52):
        a=rng.uniform(0,2*math.pi); s=rng.uniform(.53,.79)
        cx,cz=680*s*math.cos(a),580*s*math.sin(a)
        for i in range(rng.randint(9,19)):
            x,z=cx+rng.gauss(0,17),cz+rng.gauss(0,14)
            r=rng.uniform(.7,3.2) if i else rng.uniform(5,9)
            stone(x,z,r,r*rng.uniform(.32,.9),rng.choice([rockm,rockm,lightrock,shale]))
    for i in range(340):
        z=rng.uniform(-335,335); side=rng.choice([-1,1])
        x=(-238+32*math.sin(z/90)+12*math.sin(z/38)) if i%2 else (257+43*math.sin(z/115+.8)+14*math.sin(z/47))
        x+=side*rng.uniform(7,24)
        r=rng.uniform(.35,1.65)
        stone(x,z,r,r*.4,lightrock if i%3 else shale)
    for i in range(100):
        a=rng.uniform(0,6.28); s=rng.uniform(.24,.58)
        stone(680*s*math.cos(a),580*s*math.sin(a),rng.uniform(1.5,4.5),rng.uniform(.6,2.4),rockm)
    def twig(a,b,r,m):
        axis=g.norm(g.sub(b,a)); ref=(0,1,0) if abs(axis[1])<.9 else (1,0,0)
        u=g.norm(g.cross(axis,ref)); v=g.cross(axis,u); verts=[]
        for point in [a,b]:
            for i in range(4):
                ang=i*math.pi/2
                verts.append(tuple(point[j]+r*(u[j]*math.cos(ang)+v[j]*math.sin(ang)) for j in range(3)))
        faces=[]
        for i in range(4):
            j=(i+1)%4;faces.extend([(i,j,j+4),(i,j+4,i+4)])
        add('BG_Desert_scrub',verts,faces,m)
    for i in range(500):
        a=rng.uniform(0,6.28); s=rng.uniform(.24,.73)
        x,z=680*s*math.cos(a),580*s*math.sin(a)
        y,_,_=field(x,z); h=rng.uniform(.5,1.5)
        for j in range(rng.randint(3,6)):
            ang=rng.uniform(0,6.28); dx,dz=math.cos(ang)*h*.8,math.sin(ang)*h*.8
            twig((x,y,z),(x+dx,y+h,z+dz),.045,grass)
    for i in range(27):
        a=rng.uniform(0,6.28); s=rng.uniform(.28,.56)
        x,z=680*s*math.cos(a),580*s*math.sin(a); y,_,_=field(x,z)
        h=rng.uniform(2,4)
        twig((x,y,z),(x+.4,y+h,z),.18,wood)
        for sign in [-1,1]:
            twig((x+.2,y+h*.45,z),(x+sign*h*.65,y+h*.9,z+.5),.10,wood)
    for (name,m),(v,f) in batches.items():g.mesh(name,v,f,m)
    return field
