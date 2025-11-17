using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Hurtbox : MonoBehaviour {
    public FighterHealth owner;
    private void Reset(){
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
        gameObject.layer = LayerMask.NameToLayer("Hurtbox");
    }
}
