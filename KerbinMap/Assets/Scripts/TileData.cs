using System;
using System.Collections.Generic;

[Serializable]
public class TileData
{
    public string HexCode;
    // Conditions
    public CultureDef Culture;
    // Positional
    public UnityEngine.Vector2 Coordinates;
    public UnityEngine.Vector2 Position;
    public float Altitude;
    // KSP Biome effects movement
    public string Terrain;
    // Size
    public float Area;
    public int Population;
    // Positional
    public String ProvinceParent;
    public String ContinentParent;
    // Claim Value is an aggregate of local values
    public int ClaimValue;
}

[Serializable]
public class ProvinceData
{
    public List<TileData> Tiles = new List<TileData>();
    public string HexCode;
    // Positional
    public String ContinentParent;
    // Size for portion of values
    public float Area;
    public int Population;
}

[Serializable]
public class ContinentData
{
    public string HexCode;
    // Size
    public List<ProvinceData> Provinces = new List<ProvinceData>();
}

[Serializable]
public class MapLocalisation
{
    public string HexCode;
    public string Name;
}

[Serializable]
public class CultureDef
{
    public string HexCode;
    public string Dialect;
    public string Language;
    public string Group;
    public string Family;
}
