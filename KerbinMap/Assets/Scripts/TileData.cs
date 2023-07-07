using System;

[Serializable]
public class TileData
{
    public string HexCode;
    //Conditions
    public float Temperature;
    public string Biome;
    public string Language;
    //Positional
    public string Continent;
    public string Altitude;
    public UnityEngine.Vector2 Position;
    //Size
    public float Area;
    public int pxArea;
}
