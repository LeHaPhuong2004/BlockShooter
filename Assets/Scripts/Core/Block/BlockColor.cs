using UnityEngine;

[CreateAssetMenu(fileName = "BlockColor", menuName = "Pixel Flow/Block Color")]
public class BlockColorData : ScriptableObject
{
    [Header("Info")]
    public string colorName;

    [Header("Visual")]
    public Color color = Color.white;
    public Sprite icon;

    [Header("Gameplay")]
    public int defaultHP = 1;
    //public int scoreBonus = 10;

    //[Header("Effects")]
    //public GameObject hitEffect;
    //public GameObject destroyEffect;

    //[Header("Audio")]
    //public AudioClip hitSound;
    //public AudioClip destroySound;
}