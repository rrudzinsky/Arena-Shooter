using UnityEngine;

namespace ArenaShooter
{
    [RequireComponent(typeof(CombatantHealth))]
    [RequireComponent(typeof(WeaponInventory))]
    public sealed class PlayerTurret : MonoBehaviour
    {
        private MatchController match;
        private ArenaTheme theme;
        private ArenaLayout layout;
        private Transform player;
        private CombatantHealth health;
        private WeaponInventory weapons;
        private CharacterController mobilityController;
        private Transform head;
        private Transform muzzle;
        private Transform legRoot;
        private float nextShotAt;
        private float verticalVelocity;
        private float nextMoveDecisionAt;
        private Vector3 moveDestination;
        private bool mobilityEnabled;
        private bool deployed = true;

        public PlayerTurret Configure(MatchController owner, ArenaTheme arenaTheme)
        {
            match = owner;
            theme = arenaTheme;
            BuildVisuals();

            health = GetComponent<CombatantHealth>();
            health.Configure("Auto Turret", match != null ? match.CurrentNormalDroidHealth * 2f : 122f);
            health.Died += OnTurretDestroyed;

            var hitbox = gameObject.AddComponent<BoxCollider>();
            hitbox.center = new Vector3(0f, 0.62f, 0f);
            hitbox.size = new Vector3(1.1f, 1.25f, 1.1f);

            weapons = GetComponent<WeaponInventory>();
            weapons.Configure(health, muzzle, theme.Beam);
            weapons.Equip(new WeaponDefinition
            {
                DisplayName = "Turret Blaster",
                Damage = 13f,
                Range = 62f,
                Cooldown = 0.34f,
                Ammo = 999
            });

            return this;
        }

        public void EnableMobility(ArenaLayout arenaLayout, Transform playerTransform)
        {
            mobilityEnabled = true;
            layout = arenaLayout;
            player = playerTransform;

            if (mobilityController == null)
            {
                mobilityController = gameObject.AddComponent<CharacterController>();
                mobilityController.height = 1.1f;
                mobilityController.radius = 0.48f;
                mobilityController.center = new Vector3(0f, 0.52f, 0f);
            }

            if (legRoot == null)
            {
                legRoot = new GameObject("Spider Mobility Legs").transform;
                legRoot.SetParent(transform, false);
                AddMobilityLeg("Front Left Leg", new Vector3(-0.42f, 0.18f, 0.42f), 35f);
                AddMobilityLeg("Front Right Leg", new Vector3(0.42f, 0.18f, 0.42f), -35f);
                AddMobilityLeg("Rear Left Leg", new Vector3(-0.42f, 0.18f, -0.38f), -35f);
                AddMobilityLeg("Rear Right Leg", new Vector3(0.42f, 0.18f, -0.38f), 35f);
            }
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.Died -= OnTurretDestroyed;
            }
        }

        private void OnTurretDestroyed(CombatantHealth destroyedHealth)
        {
            Destroy(gameObject);
        }

        private void Update()
        {
            if (match == null || health == null || !health.IsAlive || !match.IsMatchActive)
            {
                return;
            }

            var target = FindVisibleTarget();
            if (target == null)
            {
                if (mobilityEnabled)
                {
                    SetDeployed(false);
                    UpdateMobility();
                    return;
                }

                if (head != null)
                {
                    head.Rotate(0f, 42f * Time.deltaTime, 0f, Space.Self);
                }

                return;
            }

            SetDeployed(true);
            var aimPoint = target.transform.position + Vector3.up * 0.72f;
            var direction = aimPoint - muzzle.position;
            if (direction.sqrMagnitude < 0.001f)
            {
                return;
            }

            var look = Quaternion.LookRotation(direction.normalized, Vector3.up);
            if (head != null)
            {
                head.rotation = Quaternion.RotateTowards(head.rotation, look, 620f * Time.deltaTime);
            }

            if (Time.time >= nextShotAt && Vector3.Angle(muzzle.forward, direction.normalized) < 14f)
            {
                nextShotAt = Time.time + Random.Range(0.2f, 0.36f);
                weapons.TryFire(muzzle.position, direction.normalized);
            }
        }

        private void UpdateMobility()
        {
            if (mobilityController == null)
            {
                return;
            }

            if (Time.time >= nextMoveDecisionAt || FlatDistance(transform.position, moveDestination) <= 1.15f)
            {
                nextMoveDecisionAt = Time.time + Random.Range(0.85f, 1.35f);
                moveDestination = ChooseMobilityDestination();
            }

            var toDestination = moveDestination - transform.position;
            toDestination.y = 0f;
            if (toDestination.sqrMagnitude <= 1.2f)
            {
                ApplyMobilityGravity(Vector3.zero);
                return;
            }

            var direction = toDestination.normalized;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction, Vector3.up), 360f * Time.deltaTime);
            ApplyMobilityGravity(direction * 2.85f);

            if (legRoot != null)
            {
                legRoot.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 9f) * 3.5f, 0f);
            }
        }

        private Vector3 ChooseMobilityDestination()
        {
            var nearestDroid = FindNearestDroid();
            if (nearestDroid != null)
            {
                var away = transform.position - nearestDroid.transform.position;
                away.y = 0f;
                if (away.sqrMagnitude < 0.1f)
                {
                    away = -transform.forward;
                }

                return nearestDroid.transform.position + away.normalized * 8f;
            }

            if (player != null && FlatDistance(transform.position, player.position) > 7f)
            {
                return player.position - player.forward * 3f + player.right * Random.Range(-2.2f, 2.2f);
            }

            return transform.position;
        }

        private CombatantHealth FindNearestDroid()
        {
            CombatantHealth best = null;
            var bestDistance = float.PositiveInfinity;
            if (match == null)
            {
                return null;
            }

            foreach (var droid in match.ActiveDroids)
            {
                if (droid == null || !droid.IsAlive)
                {
                    continue;
                }

                var distance = (droid.transform.position - transform.position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = droid;
                }
            }

            return best;
        }

        private void ApplyMobilityGravity(Vector3 planarVelocity)
        {
            verticalVelocity += Physics.gravity.y * Time.deltaTime;
            if (mobilityController.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -1f;
            }

            planarVelocity.y = verticalVelocity;
            mobilityController.Move(planarVelocity * Time.deltaTime);
        }

        private void SetDeployed(bool shouldDeploy)
        {
            deployed = shouldDeploy;
            if (head == null)
            {
                return;
            }

            var targetY = deployed ? 0.64f : 0.82f;
            var local = head.localPosition;
            local.y = Mathf.MoveTowards(local.y, targetY, Time.deltaTime * 2.8f);
            head.localPosition = local;
        }

        private CombatantHealth FindVisibleTarget()
        {
            CombatantHealth best = null;
            var bestScore = float.PositiveInfinity;
            foreach (var droid in match.ActiveDroids)
            {
                if (droid == null || !droid.IsAlive)
                {
                    continue;
                }

                var toTarget = droid.transform.position - transform.position;
                var distance = toTarget.sqrMagnitude;
                if (distance > 52f * 52f || distance >= bestScore)
                {
                    continue;
                }

                var aimPoint = droid.transform.position + Vector3.up * 0.72f;
                if (!HasLineOfSight(droid, aimPoint))
                {
                    continue;
                }

                bestScore = distance;
                best = droid;
            }

            return best;
        }

        private bool HasLineOfSight(CombatantHealth target, Vector3 aimPoint)
        {
            if (muzzle == null || target == null)
            {
                return false;
            }

            var toTarget = aimPoint - muzzle.position;
            if (toTarget.sqrMagnitude < 0.01f)
            {
                return true;
            }

            var hits = Physics.RaycastAll(muzzle.position, toTarget.normalized, toTarget.magnitude + 0.18f, ~0, QueryTriggerInteraction.Ignore);
            if (hits.Length == 0)
            {
                return true;
            }

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (var i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                var hitHealth = hit.collider.GetComponentInParent<CombatantHealth>();
                if (hitHealth == target)
                {
                    return true;
                }

                if (hitHealth != null)
                {
                    return false;
                }

                var destructible = hit.collider.GetComponentInParent<DestructibleArenaPiece>();
                if (destructible != null && destructible.AllowsProjectilePassThrough(hit.point, hit.normal))
                {
                    continue;
                }

                if (hit.collider.isTrigger)
                {
                    continue;
                }

                return false;
            }

            return false;
        }

        public static void BuildVisuals(Transform root, ArenaTheme theme, bool preview)
        {
            var baseMaterial = preview ? CreatePreviewMaterial(true) : theme.Pillar;
            var frameMaterial = preview ? baseMaterial : theme.Wall;
            var armorMaterial = preview ? baseMaterial : theme.DroidArmor;
            var jointMaterial = preview ? baseMaterial : theme.DroidJoint;
            var glowMaterial = preview ? CreatePreviewMaterial(false) : theme.NeonA;

            CreatePart("Turret Base", PrimitiveType.Cylinder, baseMaterial, root, new Vector3(0f, 0.14f, 0f), new Vector3(0.52f, 0.14f, 0.52f), Vector3.zero);
            CreatePart("Turret Tripod A", PrimitiveType.Cube, frameMaterial, root, new Vector3(0f, 0.08f, 0.52f), new Vector3(0.12f, 0.08f, 0.9f), new Vector3(0f, 0f, 0f));
            CreatePart("Turret Tripod B", PrimitiveType.Cube, frameMaterial, root, new Vector3(0.45f, 0.08f, -0.25f), new Vector3(0.12f, 0.08f, 0.82f), new Vector3(0f, 118f, 0f));
            CreatePart("Turret Tripod C", PrimitiveType.Cube, frameMaterial, root, new Vector3(-0.45f, 0.08f, -0.25f), new Vector3(0.12f, 0.08f, 0.82f), new Vector3(0f, -118f, 0f));

            var headRoot = new GameObject("Turret Head").transform;
            headRoot.SetParent(root, false);
            headRoot.localPosition = new Vector3(0f, 0.64f, 0f);
            CreatePart("Turret Body", PrimitiveType.Cube, jointMaterial, headRoot, new Vector3(0f, 0f, 0.04f), new Vector3(0.52f, 0.32f, 0.48f), Vector3.zero);
            CreatePart("Turret Armor Hood", PrimitiveType.Cube, armorMaterial, headRoot, new Vector3(0f, 0.12f, 0.02f), new Vector3(0.58f, 0.12f, 0.52f), Vector3.zero);
            CreatePart("Turret Twin Barrel L", PrimitiveType.Cylinder, armorMaterial, headRoot, new Vector3(-0.13f, 0.02f, 0.42f), new Vector3(0.035f, 0.24f, 0.035f), new Vector3(90f, 0f, 0f));
            CreatePart("Turret Twin Barrel R", PrimitiveType.Cylinder, armorMaterial, headRoot, new Vector3(0.13f, 0.02f, 0.42f), new Vector3(0.035f, 0.24f, 0.035f), new Vector3(90f, 0f, 0f));
            CreatePart("Turret Scan Glow", PrimitiveType.Cube, glowMaterial, headRoot, new Vector3(0f, 0.08f, 0.31f), new Vector3(0.36f, 0.035f, 0.025f), Vector3.zero);
        }

        private void BuildVisuals()
        {
            BuildVisuals(transform, theme, false);
            head = transform.Find("Turret Head");

            var muzzleObject = new GameObject("Turret Muzzle");
            muzzleObject.transform.SetParent(head, false);
            muzzleObject.transform.localPosition = new Vector3(0f, 0.02f, 0.68f);
            muzzle = muzzleObject.transform;

            var light = gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.None;
            light.color = new Color(0.1f, 0.85f, 1f);
            light.range = 3.2f;
            light.intensity = 1.2f;

            VerticalMarkerBeam.Attach(
                transform,
                "Green Friendly Turret Location Beam",
                new Color(0.18f, 1f, 0.38f),
                13f,
                0.58f,
                4.4f);
        }

        private void AddMobilityLeg(string objectName, Vector3 localPosition, float yaw)
        {
            if (legRoot == null)
            {
                return;
            }

            CreatePart(objectName + " Upper", PrimitiveType.Cylinder, theme.DroidJoint, legRoot, localPosition, new Vector3(0.045f, 0.34f, 0.045f), new Vector3(68f, yaw, 0f));
            CreatePart(objectName + " Foot", PrimitiveType.Sphere, theme.DroidArmor, legRoot, localPosition + new Vector3(Mathf.Sign(localPosition.x) * 0.38f, -0.12f, Mathf.Sign(localPosition.z) * 0.38f), new Vector3(0.11f, 0.055f, 0.15f), Vector3.zero);
        }

        private static Material CreatePreviewMaterial(bool body)
        {
            var material = new Material(Shader.Find("Standard"));
            material.name = body ? "Turret Placement Preview Body" : "Turret Placement Preview Glow";
            material.color = body ? new Color(0.94f, 0.97f, 1f, 0.42f) : new Color(1f, 1f, 1f, 0.56f);
            material.SetFloat("_Mode", 3f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            if (!body)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.white * 0.65f);
            }

            material.renderQueue = 3000;
            return material;
        }

        private static void CreatePart(string objectName, PrimitiveType type, Material material, Transform parent, Vector3 localPosition, Vector3 localScale, Vector3 localRotation)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = objectName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.transform.localRotation = Quaternion.Euler(localRotation);

            if (part.TryGetComponent<Collider>(out var collider))
            {
                Destroy(collider);
            }

            if (part.TryGetComponent<Renderer>(out var renderer))
            {
                renderer.sharedMaterial = material;
            }
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
