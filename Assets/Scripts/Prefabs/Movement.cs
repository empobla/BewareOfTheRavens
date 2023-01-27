// ISTA 425 / INFO 525 Algorithms for Games
//
// Sample code file

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Movement : MonoBehaviour
{
    [Tooltip("Speed of the enemy type")]
    public float velocity = 1.0f;

    [Tooltip("Direction of the enemy type")]
    public Vector3 direction = Vector3.zero;

    [Tooltip("Max time to live for enemy type (sec)")]
    public float timeToLive = 10.0f;

    [Tooltip("Time for enemy type to fade out (sec)")]
    public float fadeTime = 0.5f;

    [Tooltip("Level of ground at character layer")]
    public float groundLevel = -2.0f;

    Animator objectAnim;
    int dieTrigger;

    // Start is called before the first frame update
    void Start()
    {
        objectAnim = GetComponent<Animator>();
        dieTrigger = Animator.StringToHash("isDying");

        // randomize impact points slightly for increased realism
        groundLevel = groundLevel + Random.Range(-1.0f, 1.0f);
    }

    // Update is called once per frame
    void Update()
    {
        // decrement the lifetime counter for this tick
        timeToLive -= Time.deltaTime;
        //Debug.Log("Time to live: " + timeToLive);

        // Move the target along its heading (direction)
        // Note: must convert the direction vector, which 
        // was specified in Tworld, into a Tlocal direction
        if (transform.position.y > groundLevel)
        {
            Vector3 dirLocal = transform.worldToLocalMatrix * direction;
            transform.Translate(dirLocal * velocity * Time.deltaTime);
        }
        else 
        {
            objectAnim.SetTrigger(dieTrigger);
            gameObject.GetComponent<BoxCollider2D>().enabled = false;
        }

        if (timeToLive <= fadeTime)
        {
            SpriteRenderer renderer = GetComponent<SpriteRenderer>();

            // fade the torpedo out of existence
            renderer.color = new Color(1.0f, 1.0f, 1.0f, (renderer.color.a - (Time.deltaTime / fadeTime)));

            if (timeToLive <= 0.001f)
                // remove the object from the game
                GameObject.Destroy(gameObject);
        }
    }
}
