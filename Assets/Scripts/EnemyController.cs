// ISTA 425 / INFO 525 Algorithms for Games
//
// Enemy Controller

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyController : MonoBehaviour
{
    [Tooltip("Raven prefab type")]
    public GameObject RavenType;

    [Tooltip("Max number of ravens that may be in play at any time")]
    public int MaxRavens = 8;

    [Tooltip("Max number of ravens that may enter the scene per second")]
    public float RavenRate = 1.0f;

    [Tooltip("Fireball prefab type")]
    public GameObject FireballType;

    [Tooltip("Max number of fireballs that may be in play at any time")]
    public int   MaxFireballs = 8;

    [Tooltip("Max number of fireballs that may enter the scene per second")]
    public float FireballRate = 0.5f;

    // Maximum number of ravens that may be in play at a time
    private GameObject[] RavenArray;
    int ravenIndex = 0;

    // Maximum number of fireballs that may be in play at a time
    private GameObject[] FireArray;
    int fireIndex = 0;

    // Rate limiting timers for spawn method
    float ravenTime = 0.0f;
    float fireTime = 0.0f;

    GameController eventSystem;
    float gravity;
    List<DyingRaven> dyingRavens = new List<DyingRaven>();

    private GameObject SpawnEnemy(ref GameObject[] array, GameObject type, Vector4 position, Quaternion rotation, Vector3 direction, 
                            Vector2 xRandRange, Vector2 yRandRange, ref int index, int maxObjs, ref float timer, float rate)
    {   
        Movement mover;

        timer = 0.0f;

        if (array[index] != null)
            GameObject.Destroy(array[index]);

        Vector4 pos = transform.localToWorldMatrix * position;

        Vector3 randPos = position;
        randPos.x = position.x + Random.Range (xRandRange.x, xRandRange.y);
        randPos.y = position.y + Random.Range (yRandRange.x, yRandRange.y);

        GameObject enemy = Instantiate(type, randPos, rotation) as GameObject;
        // enemy.tag = "Enemy";
        // set the parent object to be the enemies object 
        enemy.transform.parent = this.transform;
        array[index] = enemy;

        // set the direction that the fireball is facing
        // note: direction is relative to world space
        mover = array[index].GetComponent<Movement>();
        mover.direction = direction;

        //Debug.Log("Fireball direction is " + mover.direction);

        // increment object index
        index++;
        if (index >= maxObjs)
            index = 0;
        
        return enemy;
    }

    private void SpawnEnemy(string type, ref float timer, float rate)
    {
        // check if the instantaneous rate meets constraint
        if (timer > 1.0f / rate)
        {
            if (type == "Raven")
            {
                GameObject enemy = SpawnEnemy (ref RavenArray, RavenType,
                   // Set world position, rotation, and direction (heading)
                   new Vector4(0.0f, 0.0f, 0.0f, 1.0f), Quaternion.identity, new Vector3(-1.0f, 0.0f, 0.0f),
                   // Spawning position randomized offsets (x, y)
                   new Vector2(15.0f, 15.0f), new Vector2(-3.9f, 5.0f),
                   // Indices and rate counters
                   ref ravenIndex, MaxRavens, ref timer, rate);
                enemy.tag = type;
                enemy.name = type + enemy.GetInstanceID();
            }

            if (type == "Fireball")
            {
                GameObject enemy = SpawnEnemy (ref FireArray, FireballType,
                   // Set world position, rotation, and direction (heading)
                   new Vector4(0.0f, 0.0f, 0.0f, 1.0f), Quaternion.Euler(0.0f, 0.0f, -135.0f), new Vector3(-1.0f,-1.0f, 0.0f),
                   // Spawning position randomized offsets (x, y)
                   new Vector2(0.0f, 20.0f), new Vector2(5.0f, 5.0f),
                   // Indices and rate counters
                   ref fireIndex, MaxFireballs, ref timer, rate);
                enemy.tag = type;
                enemy.name = type + enemy.GetInstanceID();
            }
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        RavenArray = new GameObject[MaxRavens];
        FireArray  = new GameObject[MaxFireballs];
        
        eventSystem = GameObject.FindGameObjectWithTag("GameController").GetComponent<GameController>();
        gravity = eventSystem.gravity;
    }

    // Update is called once per frame
    void Update()
    {
        ravenTime += Time.deltaTime;
        fireTime  += Time.deltaTime;

        // Spawn enemies: Ravens and Fireballs
        SpawnEnemy ("Raven", ref ravenTime, RavenRate);
        SpawnEnemy ("Fireball", ref fireTime, FireballRate);
      
        // Kill ravens on collision with fireball
        bool collisionHappened = eventSystem.collisions.Count > 0;
        if (collisionHappened) 
            HandleCollisions();

        KillRavens();
    }

    void HandleCollisions()
    {
        foreach(GameController.Collision col in eventSystem.collisions)
        {
            bool ravenAndFireball = col.go1.tag == "Raven" && col.go2.tag == "Fireball";
            bool fireballAndRaven = col.go1.tag == "Fireball" && col.go2.tag == "Raven";
            bool notRavenAndFireball = !(ravenAndFireball || fireballAndRaven);
            
            if (notRavenAndFireball) continue;
            
            // Get the raven
            GameObject raven = col.go1.tag == "Raven" ? col.go1 : col.go2;

            // Check if raven is in the list of dying ravens. If it is, do nothing
            bool isInList = false;
            foreach(DyingRaven dyingRaven in dyingRavens)
                if (dyingRaven.InstanceId == raven.GetInstanceID()) 
                {
                    isInList = true;
                    break;
                }
            if (isInList) continue;

            // If raven is not in the dying raven list, add it to the list
            raven.gameObject.tag = "DeadRaven";
            dyingRavens.Add(new DyingRaven(raven));

            // Freeze raven animation
            raven.GetComponent<Animator>().enabled = false;
        }
    }

    class DyingRaven {
        public int InstanceId;
        public GameObject gameObject { get; set; }
        public Vector3 fallVelocity = new Vector3(0, 0, 0);
        float groundY = -4.2f;

        public DyingRaven(GameObject gameObject)
        {
            this.gameObject = gameObject;
            this.InstanceId = gameObject.GetInstanceID();
        }

        public bool IsGrounded() { return Mathf.Abs(groundY - gameObject.transform.position.y) <= 0.1; }
    }

    void KillRavens()
    {
        // Remove all ravens that have been destroyed
        dyingRavens = dyingRavens.FindAll(raven => raven.gameObject != null);

        for(int i = 0; i < dyingRavens.Count; i++)
        {
            DyingRaven raven = dyingRavens[i];

            // Apply gravity to raven if it is in the air
            if (!raven.IsGrounded())
            {
                raven.fallVelocity.y += gravity * Time.deltaTime;
                raven.gameObject.transform.position += raven.fallVelocity * Time.deltaTime;
                continue;
            }
            
            // If raven is grounded, stop it's movement and remove it from dying raven list
            raven.gameObject.GetComponent<Movement>().enabled = false;
            dyingRavens.Remove(raven);
            i -= 1;
        }
    }
}
