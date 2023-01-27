// ISTA 425 / INFO 525 Algorithms for Games
//
// Game Controller

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameController : MonoBehaviour
{
    public List<Collision> collisions;
    public enum AxisType
    {
        X,
        Y
    }

    public enum ControlType
    {
        Cast,
        Hack,
        Jump,
        Die,
        Quit
    }

    [System.Serializable]
    public class InputMapping
    {
        [Tooltip("Game control type")]
        public ControlType type;
        [Tooltip("System key code")]
        public KeyCode key;
    }

    [Tooltip("Acceleration due to gravity")]
    public float gravity = -9.81f * 2;

    [Tooltip("Array of input mappings to player action types")]
    public InputMapping[] inputMappingArray;

    [Tooltip("Horizontal tiles of background (background width)")]
    public int numTiles = 3;

    // This is a fudge factor because the tiles are not exactly equal
    // to the camera width and I don't feel like setting a new pixel 
    // scale, reimporting and realigning all of the background layers.

    [Tooltip("Boundary padding when background doesn't exactly match camera FOV")]
    public float padding = 0.8f;

    public Vector3 scrollerMove;
    public Vector3 playerMove;

    private float layerWidth;

    // This class is used internally to query and update inputs and
    // enforces a one to one mapping between input keys and system
    // functions.
    private class InputStatus
    {
        public KeyCode key;
        public bool    status = false;
    }
    // Inputs for the x, y axes of player motion
    private Vector2 inputAxes;
    // Dictionary of all over valid input types
    private Dictionary<ControlType, InputStatus> inputStatusDictionary;

    // This method may be helpful to map player position to valid scrolling
    // range. Prevents player from leaving the left or right side of a map
    // as per clamp algorithm given in class (see GPAT Ch. 2).
    public float clamp(float pos)
    {
        float clampedPos;

        // Equal to half the full length of the tiles, (n * width) / 2
        float halfLength = ((float) numTiles) * layerWidth / 2.0f;
        // The left and right bounds minus the half screen padding area
        float  leftBound = -(halfLength - layerWidth / 2.0f - padding);
        float rightBound =  (halfLength - layerWidth / 2.0f - padding);

        if      (pos < leftBound)
            clampedPos = leftBound;
        else if (pos > rightBound)
            clampedPos = rightBound;
        else
            clampedPos = pos;

        return clampedPos;
    }

    public float getAxis (AxisType axis)
    {
        return inputAxes[(int) axis];
    }

    public bool getInput (ControlType type)
    {
        bool input = false;

        if (inputStatusDictionary.ContainsKey (type))
            input = inputStatusDictionary[type].status;

        return input;
    }

    public void updateInput ()
    {
        inputAxes[0] = Input.GetAxisRaw("Horizontal");
        inputAxes[1] = Input.GetAxisRaw("Vertical");

        foreach (ControlType type in System.Enum.GetValues(typeof(ControlType)))
        {
            if (inputStatusDictionary.ContainsKey(type))
                inputStatusDictionary[type].status = Input.GetKeyDown(inputStatusDictionary[type].key);
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        GameObject foreground = GameObject.FindGameObjectWithTag("Foreground");
        layerWidth = foreground.GetComponent<SpriteRenderer>().bounds.size.x;

        scrollerMove = Vector3.zero;
        playerMove   = Vector3.zero;

        // Initialize motion axes and 1:1 mapping of keycode to status
        inputAxes = Vector2.zero;
        inputStatusDictionary = new Dictionary<ControlType, InputStatus> ();
        foreach (InputMapping mapping in inputMappingArray)
        {
            if (!inputStatusDictionary.ContainsKey (mapping.type))
                inputStatusDictionary[mapping.type] = new InputStatus ();

            inputStatusDictionary[mapping.type].key = mapping.key;
        }

        collisions = new List<Collision>();
    }

    // Update is called once per frame
    void Update()
    {
        updateInput();

        // Handle quit
        if (getInput(ControlType.Quit))
            Application.Quit();
        
        // Get all game objects that can collide
        GameObject[] ravens = GameObject.FindGameObjectsWithTag("Raven");
        GameObject[] fireballs = GameObject.FindGameObjectsWithTag("Fireball");
        GameObject player = GameObject.FindGameObjectsWithTag("Player")[0];
        player.name = "Player" + player.GetInstanceID();

        List<GameObject> gameObjects = new List<GameObject>();
        gameObjects.Add(player);
        foreach (GameObject raven in ravens)
            gameObjects.Add(raven);
        foreach (GameObject fireball in fireballs)
            gameObjects.Add(fireball);

        // Find collisions
        collisions.Clear();
        sweepAndPrune(gameObjects);
    }

    private class EndPoint {
        public float value { get; set; }
        public bool isMin { get; set; }
        public GameObject gObject { get; set; }

        public int id { get; set; }

        public EndPoint(float value, bool isMin, GameObject gObject) {
            this.value = value;
            this.isMin = isMin;
            this.gObject = gObject;
            this.id = gObject.GetInstanceID();
        }
    }

    public class Collision {
        public GameObject go1 { get; set; }
        public GameObject go2 { get; set; }

        public Collision(GameObject go1, GameObject go2) {
            this.go1 = go1;
            this.go2 = go2;
        }
    }

    void sweepAndPrune(List<GameObject> gameObjects)
    {
        List<EndPoint> unsortedXList = new List<EndPoint>();
        bool hack = getInput(ControlType.Hack);

        foreach (GameObject go in gameObjects) {
            float hackRange = 0;
            bool lookingRight = false;

            if (go.tag == "Player")
            {
                hackRange = hack ? go.GetComponent<PlayerController>().hackRange : 0;
                lookingRight = go.GetComponent<PlayerController>().lookDirection == 1;
            }

            EndPoint xMin = new EndPoint(go.GetComponent<BoxCollider2D>().bounds.min.x - (!lookingRight ? hackRange : 0), true, go);
            EndPoint xMax = new EndPoint(go.GetComponent<BoxCollider2D>().bounds.max.x + (lookingRight  ? hackRange : 0), false, go);
            // LB EDIT: Purge dead fireballs (those that have hit the ground)
            if (xMin.value != xMax.value)
            {
                unsortedXList.Add(xMin);
                unsortedXList.Add(xMax);
            }
        }
        
        List<EndPoint> sortedX = sortValues(unsortedXList);
        List<EndPoint> activeList = new List<EndPoint>();

        for (int i = 0; i < sortedX.Count; i++) {
            EndPoint item = sortedX[i];

            // Item is a minimum
            if (item.isMin) {
                activeList.Add(item);
                continue;
            }

            // Item is not a minimum
            // Remove the minimum value for the item from the active list 
            for (int j = 0; j < activeList.Count; j++)
                if (item.id == activeList[j].id)
                    activeList.Remove(activeList[j]);

            // Check for collisions with active items on list
            foreach (EndPoint activeItem in activeList) {
                if (checkAABBIntersection(item.gObject, activeItem.gObject))
                {

                    // LB EDIT: making the intersected objects more obvious
                    // SpriteRenderer renderer1 = item.gObject.GetComponent<SpriteRenderer>();
                    // renderer1.color = new Color(0.0f, 5.0f, 0.0f, 0.4f);
                    // SpriteRenderer renderer2 = activeItem.gObject.GetComponent<SpriteRenderer>();
                    // renderer2.color = new Color(0.0f, 5.0f, 0.0f, 0.4f);

                    // EndPoint raven = item.gObject.tag == "Raven" ? item : activeItem.gObject.tag == "Raven" ? activeItem : null;
                    // if (raven != null)
                    // {
                    //     SpriteRenderer renderer3 = raven.gObject.GetComponent<SpriteRenderer>();
                    //     renderer3.color = new Color(0.0f, 5.0f, 0.0f, 0.4f);
                    // }

                    collisions.Add(new Collision(item.gObject, activeItem.gObject));

                }
            }

        }

    }

    List<EndPoint> sortValues (List<EndPoint> unsortedList)
    {
        List<EndPoint> sortedValues = new List<EndPoint>();
        foreach (EndPoint item in unsortedList)
            sortedValues.Add(item);
        
        for (int i = 2; i < sortedValues.Count; i++) {
            // LB EDIT: Save the endpoint obj ref, not just the key
            // float key = sortedValues[i].value;
            EndPoint key = sortedValues[i];

            int j = i - 1;
            while (j > 0 && sortedValues[j].value > key.value) {
                sortedValues[j + 1] = sortedValues[j];
                j--;
            }
            // LB EDIT: This was the principal problem that lead to junk sortedX list
            //sortedValues[j+1] = sortedValues[i];
            sortedValues[j+1] = key;
        }

        return sortedValues;
    }

    bool checkAABBIntersection(GameObject gameObject1, GameObject gameObject2) {
        Vector3 gameObject1Min, gameObject1Max, gameObject2Min, gameObject2Max;
        bool hack = getInput(ControlType.Hack);

        if (gameObject1.tag == "Player" && hack)
        {
            bool lookingRight = !gameObject1.GetComponent<SpriteRenderer>().flipX;
            Vector3 hackRange = Vector3.right * gameObject1.GetComponent<PlayerController>().hackRange;
            gameObject1Min = gameObject1.GetComponent<BoxCollider2D>().bounds.min - (!lookingRight ? hackRange : Vector3.zero);
            gameObject1Max = gameObject1.GetComponent<BoxCollider2D>().bounds.max + (lookingRight ? hackRange : Vector3.zero);
            gameObject2Min = gameObject2.GetComponent<BoxCollider2D>().bounds.min;
            gameObject2Max = gameObject2.GetComponent<BoxCollider2D>().bounds.max;
        }

        else if (gameObject2.tag == "Player" && hack)
        {
            bool lookingRight = !gameObject2.GetComponent<SpriteRenderer>().flipX;
            Vector3 hackRange = Vector3.right * gameObject2.GetComponent<PlayerController>().hackRange;
            gameObject1Min = gameObject1.GetComponent<BoxCollider2D>().bounds.min;
            gameObject1Max = gameObject1.GetComponent<BoxCollider2D>().bounds.max;
            gameObject2Min = gameObject2.GetComponent<BoxCollider2D>().bounds.min - (!lookingRight ? hackRange : Vector3.zero);
            gameObject2Max = gameObject2.GetComponent<BoxCollider2D>().bounds.max + (lookingRight ? hackRange : Vector3.zero);
        }

        else
        {
            gameObject1Min = gameObject1.GetComponent<BoxCollider2D>().bounds.min;
            gameObject1Max = gameObject1.GetComponent<BoxCollider2D>().bounds.max;
            gameObject2Min = gameObject2.GetComponent<BoxCollider2D>().bounds.min;
            gameObject2Max = gameObject2.GetComponent<BoxCollider2D>().bounds.max;
        }
        
        bool xIntersection = gameObject1Max.x < gameObject2Min.x || gameObject2Max.x < gameObject1Min.x;
        bool yIntersection = gameObject1Max.y < gameObject2Min.y || gameObject2Max.y < gameObject1Min.y;

        bool intersection = !(xIntersection || yIntersection);

        return intersection;
    }
}
