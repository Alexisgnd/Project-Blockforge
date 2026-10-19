using UnityEngine;

// =========================================================
// CINEMATIQUE D'ATTENTE DU MATCHMAKING (scene Matchmaking_Space)
// Jouee des l'arrivee dans la scene, sous le bandeau du Forge
// Loader ; tourne en boucle sur le dernier plan jusqu'au match.
//   1. ARRIVEE   l'espace vide ; le Super Destroyer arrive du
//                lointain et passe au-dessus de la camera, qui
//                bascule vers son ventre (vue du dessous) ;
//   2. APPROCHE  la camera remonte sur le flanc et se cale sur
//                les portes de la baie du joueur ;
//   3. PORTES    les portes de la baie s'ouvrent (haut / bas) ;
//   4. SORTIE    le vaisseau garage de la baie (1, 2 ou 3 =
//                baie du robot choisi) sort de la soute ;
//   5. DEPART    il part vers l'avant-bas du Super Destroyer,
//                en accelerant, et sort du cadre ;
//   6. POURSUITE plan sur le vaisseau garage seul, qui fonce
//                avec une flamme d'entree sur l'avant
//                (BowFlame, ReentryFlameFX), trainees de vitesse ;
//                boucle jusqu'au match ;
//   7. MATCH     MatchmakingService.MatchFound : la camera se fige,
//                le vaisseau garage accelere a fond (flamme et
//                moteurs boostes), frole la camera et sort du
//                cadre ; la transition Versus (MatchIntro) est
//                retenue (MatchmakingService.TransitionGate)
//                jusqu'a ce qu'il soit sorti. Match trouve avant
//                la poursuite : coupe directe sur elle.
// Baie : SelectedBay (pose par l'ecran Play au lancement de la
// recherche), sinon RobotSession.Slot + 1. Cote du Super
// Destroyer : celui qui fait face a la camera de depart.
// Positions mesurees a l'execution sur les noeuds du GLB
// (bay_cavity_<cote>_<n>, bay_door_upper/lower_<cote>_<n>).
// References et effets poses par
// Blockforge > Setup Matchmaking Cinematic.
// =========================================================

public class MatchmakingCinematic : MonoBehaviour
{
    // Baie du robot qui part au combat (1 a 3), posee par l'ecran Play
    public static int SelectedBay;

    [Header("Scene (rempli par le setup)")]
    public Transform carrier;
    public Camera cam;
    public Transform garageShip;
    public ShipThrusterFX[] engines = new ShipThrusterFX[0];
    public ReentryFlameFX bowFlame;
    public Light bowLight;
    public Material streakMaterial;

    [Header("Baie")]
    [Tooltip("1 a 3 pour forcer une baie (apercu) ; 0 = baie du robot choisi")]
    [Range(0, 3)] public int bayOverride;

    [Header("Temps (s)")]
    public float arriveTime = 7f;
    public float pushTime = 4.5f;
    public float doorsTime = 2f;
    public float exitTime = 3.5f;
    public float launchTime = 4f;
    public float chaseRampTime = 1.5f;

    [Header("Arrivee et approche")]
    [Tooltip("Distance parcourue par le Super Destroyer pendant l'arrivee (m)")]
    public float arriveDistance = 4500f;
    [Tooltip("Champ de depart : la camera resserre ensuite jusqu'au champ de la scene")]
    public float arriveFovStart = 42f;
    [Tooltip("Position de la camera pendant l'arrivee, par rapport au centre final du vaisseau : (cote baie, haut, avant) en m. " +
             "Devant et sous le vaisseau, cote baie : il arrive de face depuis le lointain et finit en 3/4 avant, flanc des baies visible")]
    public Vector3 arriveOffset = new(520f, -260f, 950f);
    [Tooltip("Distance camera - ouverture de la baie pendant la sortie (m)")]
    public float bayShotDistance = 360f;
    public float bayShotFov = 38f;
    [Tooltip("Direction de la camera depuis la baie a l'arrivee : (cote baie, haut, avant)")]
    public Vector3 bayShotDirection = new(0.78f, 0.22f, 0.5f);
    [Tooltip("Direction a la fin de la sortie : la camera tourne de l'une a l'autre pendant les portes et la sortie")]
    public Vector3 bayShotDirectionEnd = new(0.9f, 0.3f, 0.25f);
    [Tooltip("Distance en fin de sortie, en fraction de la distance d'arrivee (< 1 : la camera se rapproche)")]
    public float bayShotZoom = 0.8f;

    [Header("Sortie et depart (m)")]
    [Tooltip("Hauteur des berceaux sous le vaisseau garage")]
    public float cradleHeight = 2.6f;
    [Tooltip("Marge entre la coque et le vaisseau garage sorti")]
    public float exitClearance = 45f;
    [Tooltip("Point d'arrivee du depart : (cote baie, vertical, avant)")]
    public Vector3 launchOffset = new(150f, -480f, 900f);

    [Header("Poursuite")]
    [Tooltip("Camera dans le repere du vaisseau garage : (cote, haut, avant)")]
    public Vector3 chaseOffset = new(75f, 22f, 105f);
    public float chaseFov = 42f;
    [Tooltip("Piquee du vaisseau garage pendant la poursuite (degres)")]
    public float chasePitch = 12f;
    public float streakSpeed = 900f;
    public float shake = 0.35f;

    [Header("Match trouve : sortie du cadre")]
    [Tooltip("Duree avant de laisser partir la transition Versus (s)")]
    public float burstDuration = 1.4f;
    [Tooltip("Acceleration du vaisseau garage (m/s2)")]
    public float burstAcceleration = 900f;
    [Tooltip("Flamme et moteurs pendant la sortie (1 = normal)")]
    public float burstBoost = 2.5f;

    private enum Phase { Arrive, Push, Bay, Launch, Chase }

    private Phase phase;
    private float t0;
    private bool ready;

    // Mesures (Super Destroyer a sa pose finale)
    private Vector3 carrierFinal, fwd, up, side;
    private Vector3 bayOpening;
    private Vector3 camStart, belowPos, bayShotPos;
    private float camStartFov;
    private Transform doorUpper, doorLower;
    private Vector3 doorUpperLocal, doorLowerLocal, doorUpperOpen, doorLowerOpen;
    private Vector3 shipStartLocal, shipExitLocal;
    private Vector3 launchFrom, launchControl, launchTo;
    private Quaternion launchRotation;
    private float bowLightIntensity;
    private ParticleSystem speedStreaks;

    // Match trouve
    private bool burstRequested, burstDone;
    private float burstStart = -1f;
    private Vector3 burstOrigin, burstCamPos;
    private Quaternion burstCamRot;
    private float tDoors, tExit, tLaunch, tChase;

    public int Bay => bayOverride > 0 ? bayOverride
                    : SelectedBay > 0 ? Mathf.Clamp(SelectedBay, 1, 3)
                    : Mathf.Clamp(RobotSession.Slot + 1, 1, 3);

    private void OnEnable()
    {
        MatchmakingService.MatchFound += OnMatchFound;
    }

    private void OnDisable()
    {
        MatchmakingService.MatchFound -= OnMatchFound;
        if (MatchmakingService.TransitionGate == BurstFinished)
            MatchmakingService.TransitionGate = null;
    }

    // 7. Les sieges sont pleins : sortie du cadre, puis la transition peut partir
    private void OnMatchFound()
    {
        if (!ready || burstRequested)
            return;
        burstRequested = true;
        burstDone = false;
        burstStart = -1f;
        MatchmakingService.TransitionGate = BurstFinished;
        // Pas encore en poursuite : coupe directe dessus
        if (phase != Phase.Chase)
            t0 = Time.time - tChase - chaseRampTime;
    }

    private bool BurstFinished() => burstDone;

    [ContextMenu("Test : match trouve (sortie du cadre)")]
    private void TestBurst()
    {
        if (Application.isPlaying)
            OnMatchFound();
    }

    private void Start()
    {
        ready = Prepare();
        if (!ready)
        {
            Debug.LogWarning("[MatchmakingCinematic] References ou noeuds de baie introuvables : la scene reste fixe. " +
                             "Lance Blockforge > Setup Matchmaking Cinematic.");
            return;
        }
        t0 = Time.time;
        phase = Phase.Arrive;
        ApplyArrive(0f);
    }

    // =========================================================
    // MESURES
    // =========================================================

    private bool Prepare()
    {
        if (carrier == null || cam == null || garageShip == null)
            return false;

        carrierFinal = carrier.position;
        fwd = carrier.forward;
        up = carrier.up;
        camStart = cam.transform.position;
        camStartFov = cam.fieldOfView;

        int bay = Bay;
        // Cote qui fait face a la camera de depart
        bool hasStbd = TryNodeBounds($"bay_cavity_stbd_{bay}", out var stbd, out _);
        bool hasPort = TryNodeBounds($"bay_cavity_port_{bay}", out var port, out _);
        if (!hasStbd && !hasPort)
            return false;
        string sideName = !hasPort || (hasStbd && (stbd.center - camStart).sqrMagnitude <= (port.center - camStart).sqrMagnitude) ? "stbd" : "port";
        var cavity = sideName == "stbd" ? stbd : port;

        side = Vector3.Project(cavity.center - carrierFinal, carrier.right);
        side = side.sqrMagnitude > 0.01f ? side.normalized : carrier.right;
        float halfDepth = Mathf.Abs(Vector3.Dot(cavity.extents, new Vector3(Mathf.Abs(side.x), Mathf.Abs(side.y), Mathf.Abs(side.z))));
        bayOpening = cavity.center + side * halfDepth;
        bayShotPos = BayCamera(0f);

        // Portes : la haute monte, la basse descend, de leur hauteur
        if (TryNodeBounds($"bay_door_upper_{sideName}_{bay}", out var upperBounds, out doorUpper))
        {
            doorUpperLocal = doorUpper.localPosition;
            doorUpperOpen = doorUpperLocal + doorUpper.parent.InverseTransformVector(up * (upperBounds.size.y + 1f));
        }
        if (TryNodeBounds($"bay_door_lower_{sideName}_{bay}", out var lowerBounds, out doorLower))
        {
            doorLowerLocal = doorLower.localPosition;
            doorLowerOpen = doorLowerLocal - doorLower.parent.InverseTransformVector(up * (lowerBounds.size.y + 1f));
        }

        // Le GLB du Super Destroyer porte deja un vaisseau garage amarre dans chaque baie
        // (ShipGarage_<k>_<cote>_<baie>) : celui de la baie choisie est masque et
        // GarageShip (le modele complet, avec ses effets) prend exactement sa place.
        // Rotation de base : nez vers l'avant du Super Destroyer, la ou il va partir
        // (les copies du GLB sont amarrees nez vers l'arriere : on les retourne aussi).
        Vector3 shipForward = fwd;
        Bounds? docked = null;
        FaceDockedShipsForward();
        var dockedNode = FindNode(t => t.name.StartsWith("ShipGarage_") && t.name.EndsWith($"_{sideName}_{bay}"));
        if (dockedNode != null)
        {
            docked = MeasureRenderers(dockedNode);
            dockedNode.gameObject.SetActive(false);
        }

        garageShip.SetParent(null, true);
        garageShip.rotation = Quaternion.LookRotation(shipForward, up);
        var shipBounds = MeasureRenderers(garageShip);
        Vector3 pivotOffset = garageShip.position - shipBounds.center;
        float halfWidth = Mathf.Abs(Vector3.Dot(shipBounds.extents, new Vector3(Mathf.Abs(side.x), Mathf.Abs(side.y), Mathf.Abs(side.z))));
        var shipStart = docked.HasValue
            ? docked.Value.center + pivotOffset
            : new Vector3(cavity.center.x, cavity.min.y + cradleHeight + (garageShip.position.y - shipBounds.min.y), cavity.center.z);
        var shipExit = shipStart + side * (halfDepth + halfWidth + exitClearance);
        shipStartLocal = carrier.InverseTransformPoint(shipStart);
        shipExitLocal = carrier.InverseTransformPoint(shipExit);
        garageShip.SetParent(carrier, true);
        garageShip.position = shipStart;

        // Portail avant du vaisseau garage ferme avant l'ouverture des portes de la baie
        // (pose mesuree portes ouvertes ci-dessus, comme la copie amarree du GLB)
        CloseGarageDoors();

        launchFrom = shipExit;
        launchControl = shipExit + fwd * 300f;
        launchTo = shipExit + side * launchOffset.x + up * launchOffset.y + fwd * launchOffset.z;

        // Effets eteints jusqu'a leur plan
        SetEngines(false);
        if (bowFlame != null)
        {
            bowFlame.intensity = 0f;
            bowFlame.boost = 1f;
            bowFlame.gameObject.SetActive(false);
        }
        if (bowLight != null)
        {
            bowLightIntensity = bowLight.intensity;
            bowLight.gameObject.SetActive(false);
        }

        // Horaires
        tDoors = arriveTime + pushTime * 0.55f;
        tExit = tDoors + doorsTime + 0.3f;
        tLaunch = tExit + exitTime;
        tChase = tLaunch + launchTime;

        // Camera d'arrivee : devant et sous le vaisseau, cote baie (jamais sur sa trajectoire)
        belowPos = carrierFinal + side * arriveOffset.x + up * arriveOffset.y + fwd * arriveOffset.z;

        // Le Super Destroyer part de l'arriere-plan
        carrier.position = carrierFinal - fwd * arriveDistance;
        return true;
    }

    // Le modele du garage a son portail ouvert : volet du haut (« Clamshell ... ») et
    // rampe du bas (« Ramp ... ») a plat vers l'avant. Chaque piece tourne de 90 degres
    // autour de sa charniere (« Clamshell hinge » / « Ramp hinge ») : le volet descend,
    // la rampe remonte, et les deux se rejoignent devant le portail.
    private void CloseGarageDoors()
    {
        RotateLeaf("Clamshell", "Clamshell hinge", -1f);
        RotateLeaf("Ramp", "Ramp hinge", +1f);
    }

    // verticalSign : -1 = la piece doit descendre, +1 = monter
    private void RotateLeaf(string prefix, string hingeName, float verticalSign)
    {
        if (!TryChildBounds(garageShip, hingeName, out var hinge))
            return;
        var parts = new System.Collections.Generic.List<Transform>();
        bool has = false;
        Bounds leaf = default;
        foreach (var r in garageShip.GetComponentsInChildren<Renderer>(true))
        {
            string n = r.gameObject.name;
            if (!n.StartsWith(prefix) || n == hingeName || n.StartsWith(hingeName + "_"))
                continue;
            parts.Add(r.transform);
            if (!has) { leaf = r.bounds; has = true; }
            else leaf.Encapsulate(r.bounds);
        }
        if (!has)
            return;

        // Sens de rotation : celui qui amene le centre de la piece du bon cote de la charniere
        Vector3 axis = garageShip.right;
        Vector3 arm = leaf.center - hinge.center;
        Vector3 turned = Quaternion.AngleAxis(90f, axis) * arm;
        float angle = Mathf.Sign(Vector3.Dot(turned, up)) == Mathf.Sign(verticalSign) ? 90f : -90f;
        foreach (var part in parts)
        {
            // Une sous-piece (primitive eclatee par glTFast) suit deja son parent
            bool nested = false;
            for (var p = part.parent; p != null && p != garageShip; p = p.parent)
                nested |= parts.Contains(p);
            if (!nested)
                part.RotateAround(hinge.center, axis, angle);
        }
    }

    // Chaque vaisseau garage amarre (ShipGarage_*) tourne sur lui-meme, autour du centre
    // de ses bornes, pour avoir le nez (Front lintel) vers l'avant du Super Destroyer.
    private void FaceDockedShipsForward()
    {
        foreach (var t in carrier.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith("ShipGarage_"))
                continue;
            if (!TryChildBounds(t, "Front lintel", out var front) || !TryChildBounds(t, "Engine nozzle", out var rear))
                continue;
            var nose = Vector3.ProjectOnPlane(front.center - rear.center, up);
            if (nose.sqrMagnitude < 0.01f)
                continue;
            float angle = Vector3.SignedAngle(nose, fwd, up);
            if (Mathf.Abs(angle) > 1f)
                t.RotateAround(MeasureRenderers(t).center, up, angle);
        }
    }

    private Transform FindNode(System.Predicate<Transform> match)
    {
        foreach (var t in carrier.GetComponentsInChildren<Transform>(true))
        {
            if (match(t))
                return t;
        }
        return null;
    }

    // Renderers d'un sous-objet nomme (ou de ses doublons suffixes par glTFast : « Engine nozzle_1 »)
    private static bool TryChildBounds(Transform root, string name, out Bounds bounds)
    {
        bounds = default;
        bool has = false;
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            string n = r.gameObject.name;
            if (n != name && !n.StartsWith(name + "_"))
                continue;
            if (!has) { bounds = r.bounds; has = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return has;
    }

    // Camera autour de la baie : k = 0 a l'arrivee (fin de l'approche), 1 en fin de sortie
    private Vector3 BayCamera(float k)
    {
        Vector3 Dir(Vector3 d) => (side * d.x + up * d.y + fwd * d.z).normalized;
        float e = EaseInOut(k);
        Vector3 dir = Vector3.Slerp(Dir(bayShotDirection), Dir(bayShotDirectionEnd), e);
        return bayOpening + dir * (bayShotDistance * Mathf.Lerp(1f, bayShotZoom, e));
    }

    private bool TryNodeBounds(string nodeName, out Bounds bounds, out Transform node)
    {
        bounds = default;
        node = null;
        foreach (var t in carrier.GetComponentsInChildren<Transform>(true))
        {
            if (t.name != nodeName)
                continue;
            node = t;
            bool has = false;
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            {
                if (!has) { bounds = r.bounds; has = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return has;
        }
        return false;
    }

    private static Bounds MeasureRenderers(Transform root)
    {
        bool has = false;
        var b = new Bounds(root.position, Vector3.zero);
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r.GetComponentInParent<ShipThrusterFX>(true) != null
                || r.GetComponentInParent<ReentryFlameFX>(true) != null)
                continue;
            if (!has) { b = r.bounds; has = true; }
            else b.Encapsulate(r.bounds);
        }
        return b;
    }

    // =========================================================
    // DEROULE
    // =========================================================

    private void LateUpdate()
    {
        if (!ready)
            return;
        float t = Time.time - t0;

        if (t < arriveTime) ApplyArrive(t);
        else if (t < arriveTime + pushTime) ApplyPush(t);
        else if (t < tLaunch) ApplyBay(t);
        else if (t < tChase) ApplyLaunch(t);
        else ApplyChase(t);

        ApplyDoors(t);
    }

    // 1. Le Super Destroyer arrive, la camera resserre le champ
    private void ApplyArrive(float t)
    {
        phase = Phase.Arrive;
        float k = Mathf.Clamp01(t / arriveTime);
        carrier.position = carrierFinal - fwd * (arriveDistance * (1f - EaseOutCubic(k)));
        // Le vaisseau n'est d'abord qu'un point de face dans les etoiles ; il grossit en approchant
        // et s'arrete en 3/4 avant devant la camera, le flanc des baies visible. Leger resserrement du champ.
        Aim(belowPos, carrier.position, Mathf.Lerp(arriveFovStart, camStartFov, EaseInOut(k)));
    }

    // 2. La camera glisse jusqu'a la baie
    private void ApplyPush(float t)
    {
        phase = Phase.Push;
        carrier.position = carrierFinal;
        float k = EaseInOut((t - arriveTime) / pushTime);
        // Du ventre au flanc : arc autour du vaisseau (jamais a travers), puis cadrage des portes de la baie
        Vector3 pos = Vector3.Slerp(belowPos - carrierFinal, bayShotPos - carrierFinal, k) + carrierFinal;
        Vector3 target = Vector3.Lerp(carrierFinal, bayOpening, k);
        Aim(pos, target, Mathf.Lerp(camStartFov, bayShotFov, k));
    }

    // 3-4. Portes, puis sortie de la soute (le vaisseau garage suit le Super Destroyer)
    private void ApplyBay(float t)
    {
        phase = Phase.Bay;
        carrier.position = carrierFinal;

        float exit = 0f;
        if (t >= tExit)
        {
            if (!enginesOn)
                SetEngines(true);
            exit = EaseInOut((t - tExit) / exitTime);
            garageShip.localPosition = Vector3.Lerp(shipStartLocal, shipExitLocal, exit);
        }

        // La camera tourne autour de la baie pendant l'ouverture et la sortie, et suit le vaisseau qui sort
        float bayStart = arriveTime + pushTime;
        float k = Mathf.Clamp01((t - bayStart) / Mathf.Max(0.01f, tLaunch - bayStart));
        Aim(BayCamera(k) + Drift(t), Vector3.Lerp(bayOpening, garageShip.position, 0.6f * exit), bayShotFov);
    }

    // 5. Depart vers l'avant-bas, en accelerant, hors du cadre
    private void ApplyLaunch(float t)
    {
        if (phase != Phase.Launch)
        {
            phase = Phase.Launch;
            garageShip.SetParent(null, true);
            launchRotation = garageShip.rotation;
            SetEngines(true);
        }
        float u = Mathf.Clamp01((t - tLaunch) / launchTime);
        float s = u * u; // accelere
        Vector3 p = Bezier(launchFrom, launchControl, launchTo, s);
        Vector3 dir = BezierTangent(launchFrom, launchControl, launchTo, Mathf.Max(s, 0.02f));
        garageShip.position = p;
        garageShip.rotation = Quaternion.Slerp(launchRotation, Quaternion.LookRotation(dir, up), EaseInOut(Mathf.Clamp01(u * 2.5f)));

        // La camera accompagne un peu le depart puis le laisse sortir par le bas du cadre
        Aim(BayCamera(1f) + Drift(t), Vector3.Lerp(bayOpening, p, 0.6f * (1f - u)), bayShotFov);
    }

    // 6. Poursuite : le vaisseau garage seul, flamme d'entree sur l'avant
    private void ApplyChase(float t)
    {
        if (phase != Phase.Chase)
            EnterChase();

        float k = Mathf.Clamp01((t - tChase) / chaseRampTime);

        // Match trouve : la camera se fige, le vaisseau accelere et sort du cadre
        if (burstRequested && burstStart >= 0f)
        {
            ApplyBurst(t, k);
            return;
        }

        if (bowFlame != null)
        {
            bowFlame.intensity = k;
            bowFlame.boost = 1f;
        }
        if (bowLight != null)
            bowLight.intensity = bowLightIntensity * k * (1f + 0.15f * (Mathf.PerlinNoise(t * 9f, 3.1f) * 2f - 1f));

        // Leger balancement autour du vaisseau + tremblement
        float sway = Mathf.Sin((t - tChase) * 0.18f) * 10f;
        var orbit = Quaternion.AngleAxis(sway, garageShip.up);
        Vector3 offset = orbit * (garageShip.right * chaseOffset.x + garageShip.up * chaseOffset.y + garageShip.forward * chaseOffset.z);
        Vector3 jitter = new Vector3(Mathf.PerlinNoise(t * 7f, 0.3f) - 0.5f, Mathf.PerlinNoise(1.7f, t * 7f) - 0.5f, 0f) * (2f * shake);
        Aim(garageShip.position + offset + cam.transform.rotation * jitter, garageShip.position + garageShip.forward * 25f, chaseFov);

        if (burstRequested)
        {
            // Premiere image apres la demande : on fige la camera la ou elle est
            burstStart = t;
            burstOrigin = garageShip.position;
            burstCamPos = cam.transform.position;
            burstCamRot = cam.transform.rotation;
        }
    }

    // 7. Sortie du cadre : la camera reste en place, le vaisseau fonce et la frole
    private void ApplyBurst(float t, float k)
    {
        float u = t - burstStart;
        float ramp = Mathf.Clamp01(u / 0.25f);
        float b = Mathf.Lerp(1f, burstBoost, ramp);

        garageShip.position = burstOrigin + garageShip.forward * (0.5f * burstAcceleration * u * u);

        if (bowFlame != null)
        {
            bowFlame.intensity = Mathf.Max(k, ramp);
            bowFlame.boost = b;
        }
        if (bowLight != null)
            bowLight.intensity = bowLightIntensity * b * (1f + 0.2f * (Mathf.PerlinNoise(t * 14f, 3.1f) * 2f - 1f));
        foreach (var e in engines)
        {
            if (e != null)
                e.transform.localScale = new Vector3(1f, 1f, b);
        }
        if (speedStreaks != null)
        {
            var main = speedStreaks.main;
            main.simulationSpeed = 1f + 2.5f * ramp;
        }

        // Tremblement qui monte avec l'acceleration, puis la camera reste seule dans le vide
        float sh = shake * (1f + 4f * ramp) * (1f - Mathf.Clamp01((u - 0.8f) / 0.6f));
        Vector3 jitter = new Vector3(Mathf.PerlinNoise(t * 23f, 0.3f) - 0.5f, Mathf.PerlinNoise(1.7f, t * 23f) - 0.5f, 0f) * (2f * sh);
        cam.transform.SetPositionAndRotation(burstCamPos + burstCamRot * jitter, burstCamRot);
        cam.fieldOfView = chaseFov;

        if (u >= burstDuration)
            burstDone = true;
    }

    private void EnterChase()
    {
        phase = Phase.Chase;
        // Plan serre sur le vaisseau garage : le Super Destroyer n'est plus a l'image
        carrier.gameObject.SetActive(false);
        garageShip.SetParent(null, true);
        garageShip.SetPositionAndRotation(Vector3.zero, Quaternion.LookRotation(Quaternion.AngleAxis(chasePitch, Vector3.right) * Vector3.forward, Vector3.up));
        SetEngines(true);
        if (bowFlame != null)
        {
            bowFlame.intensity = 0f;
            bowFlame.boost = 1f;
            bowFlame.gameObject.SetActive(true);
        }
        if (bowLight != null)
            bowLight.gameObject.SetActive(true);
        CreateStreaks();
    }

    private void ApplyDoors(float t)
    {
        float k = EaseInOut((t - tDoors) / doorsTime);
        if (doorUpper != null)
            doorUpper.localPosition = Vector3.Lerp(doorUpperLocal, doorUpperOpen, k);
        if (doorLower != null)
            doorLower.localPosition = Vector3.Lerp(doorLowerLocal, doorLowerOpen, k);
    }

    // =========================================================
    // EFFETS
    // =========================================================

    private bool enginesOn;

    private void SetEngines(bool on)
    {
        enginesOn = on;
        foreach (var e in engines)
        {
            if (e != null)
                e.gameObject.SetActive(on);
        }
    }

    // Trainees de vitesse : particules etirees qui arrivent de l'avant et filent le long du vaisseau
    private void CreateStreaks()
    {
        if (streakMaterial == null)
            return;
        var go = new GameObject("SpeedStreaks");
        go.transform.SetParent(garageShip, false);
        go.transform.localPosition = new Vector3(0f, 0f, 700f);
        go.transform.localRotation = Quaternion.LookRotation(Vector3.back, Vector3.up);

        var system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main;
        main.loop = true;
        main.startLifetime = 1.6f;
        main.startSpeed = streakSpeed;
        main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 2000;
        main.prewarm = true;

        var emission = system.emission;
        emission.rateOverTime = 260f;

        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(520f, 320f, 20f);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = streakMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.08f;
        renderer.lengthScale = 1f;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        system.Play();
        speedStreaks = system;
    }

    // =========================================================
    // OUTILS
    // =========================================================

    private void Aim(Vector3 position, Vector3 target, float fov) => Aim(position, target, fov, up);

    private void Aim(Vector3 position, Vector3 target, float fov, Vector3 cameraUp)
    {
        var dir = target - position;
        cam.transform.SetPositionAndRotation(position, dir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(dir, cameraUp) : cam.transform.rotation);
        cam.fieldOfView = fov;
    }

    private Vector3 Drift(float t) => new Vector3(Mathf.Sin(t * 0.21f), Mathf.Sin(t * 0.17f + 1.3f) * 0.6f, Mathf.Cos(t * 0.13f)) * 6f;

    private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float s)
    {
        float r = 1f - s;
        return r * r * a + 2f * r * s * b + s * s * c;
    }

    private static Vector3 BezierTangent(Vector3 a, Vector3 b, Vector3 c, float s) => 2f * (1f - s) * (b - a) + 2f * s * (c - b);

    private static float EaseInOut(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }

    private static float EaseOutCubic(float x)
    {
        x = 1f - Mathf.Clamp01(x);
        return 1f - x * x * x;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        SelectedBay = 0;
    }
}
