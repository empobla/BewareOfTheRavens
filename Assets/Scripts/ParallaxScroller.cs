// ISTA 425 / INFO 525 Algorithms for Games
//
// Parallax Scroller

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParallaxScroller : MonoBehaviour
{
    [Tooltip("Camera to which parallax is relative")]
    public GameObject parallaxCamera;

    [Tooltip("Level of parallax for this depth layer")]
    public float parallaxLevel;

    GameController eventSystem;

    float startPos;
    float layerWidth;

    // Start is called before the first frame update
    void Start()
    {
        eventSystem = GameObject.FindGameObjectWithTag("GameController").GetComponent<GameController>();

        startPos = transform.position.x;

        SoundManager.Instance.PlaySound(SoundManager.SoundType.Night, true);
        
        if (gameObject.tag != "EnemyContainer")
            layerWidth = gameObject.GetComponent<SpriteRenderer>().bounds.size.x;
    }

    // Update is called once per frame
    void Update()
    {
        // TODO: Part of the parallax scrolling algorithm may go here.
        float playerMove = eventSystem.playerMove.x;
        float totalPlayerMovement = startPos - playerMove * parallaxLevel;  // Paralax to make it relative to the layer (position)

        if (totalPlayerMovement >= layerWidth)
            startPos -= layerWidth;

        if (totalPlayerMovement <= -layerWidth)
            startPos += layerWidth;
        
        transform.position = new Vector3(startPos - (playerMove * parallaxLevel), transform.position.y, transform.position.z);

    }
}
