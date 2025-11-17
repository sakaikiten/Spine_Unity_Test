using UnityEngine;

public class FighterHealth : MonoBehaviour {
    [Min(1)] public int maxHP = 100;
    public int hp = 100;

    void Awake(){ hp = Mathf.Clamp(hp, 1, maxHP); }

    public void TakeDamage(int dmg){
        hp = Mathf.Max(0, hp - Mathf.Max(0, dmg));
        Debug.Log($"{name} HP: {hp}");
        // 0でダウン演出などは後で
    }
}
