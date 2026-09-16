"""Continuous exterior plain ending in a broad, closed ridge that masks its edge."""
import math

def build(g):
    segments=224
    rings=88
    vertices=[]
    for ring in range(rings+1):
        t=ring/rings
        for i in range(segments):
            a=2*math.pi*i/segments
            # Shared angular samples keep the plain and the outer hills watertight.
            outline=1+.022*math.sin(5*a)+.016*math.sin(9*a+.8)
            rx=96+(680*outline-96)*t
            rz=77+(580*outline-77)*t
            x,z=rx*math.cos(a),rz*math.sin(a)
            if t<.10:
                # Tuck under the original floor; no step or gap at the arena edge.
                y=-.30
            else:
                fade=min(1,(t-.10)/.16)
                y=-.30+fade*(1.05*math.sin(x/73)*math.sin(z/91)+.65*math.sin((x+z)/109))
            # Several hundred metres of almost flat ground precede the horizon.
            onset=.57+.025*math.sin(3*a+.5)
            crest=.84+.025*math.sin(4*a)
            height=126+27*math.sin(3*a+.4)+15*math.sin(7*a+1.3)
            if t>onset:
                if t<=crest:
                    u=(t-onset)/(crest-onset)
                    hill=u*u*(3-2*u)
                else:
                    u=(t-crest)/(1-crest)
                    hill=1-.83*u*u*(3-2*u)
                y+=height*hill
                y+=7*math.sin(11*a+t*8)*math.sin(math.pi*max(0,min(1,(t-onset)/(1-onset))))**2
            vertices.append((x,y,z))
    faces=[]
    for ring in range(rings):
        for i in range(segments):
            j=(i+1)%segments
            a=ring*segments+i; b=ring*segments+j
            c=(ring+1)*segments+j; d=(ring+1)*segments+i
            faces.extend([(a,b,c),(a,c,d)])
    g.mesh('BG_Terrain_Continuous_plain_and_horizon_hills',vertices,faces,
           next(i for i,m in enumerate(g.materials) if m['name']=='Warm canyon dust'))
    # Backside skirt lies beyond the skyline, hidden from the playable ground.
    skirt=[]; skirtfaces=[]
    for i in range(segments):
        x,y,z=vertices[rings*segments+i]
        skirt.extend([(x,y,z),(x,-40,z)])
    for i in range(segments):
        j=(i+1)%segments
        skirtfaces.extend([(2*i,2*i+1,2*j+1),(2*i,2*j+1,2*j)])
    g.mesh('BG_Outer_backside_skirt',skirt,skirtfaces,
           next(i for i,m in enumerate(g.materials) if m['name']=='Warm canyon dust'))
