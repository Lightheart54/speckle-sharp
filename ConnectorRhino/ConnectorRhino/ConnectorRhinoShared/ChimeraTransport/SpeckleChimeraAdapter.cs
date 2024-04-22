using System;
using System.Collections.Generic;
using System.Linq;
using Chimera;
using Chimera.DataAccess;
using Speckle.Core.Kits;
using Speckle.Core.Models;

namespace ConnectorRhinoShared.ChimeraTransport;

public static class SpeckleChimeraAdapter
{
  private const string CHIMERA_OBJECT_ID_KEY = "ChimeraId";
  private const string CHIMERA_VALUE_KEY = "ChimeraValue";
  public const string CHIMERA_KEY = "Chimera";
  
  public static Base ChimeraToSpeckle(Chimera.DataAccess.IChimeraUserData cuo, ISpeckleConverter converter)
  {
    var chimeraBase = new Base();
    chimeraBase[CHIMERA_OBJECT_ID_KEY] = cuo.ObjectId;
    var chimeraDataDict = cuo.GetChimeraData();
    foreach (IChimeraData data in chimeraDataDict.Values)
    {
      chimeraBase[data.Definition.Name] = ChimeraDataToSpeckle(data,converter);
    }
    return chimeraBase;
  }

  static Base ChimeraDataToSpeckle(IChimeraData chimeraData, ISpeckleConverter converter)
  {
    var chimeraBase = new Base();
    chimeraBase[CHIMERA_OBJECT_ID_KEY] = chimeraData.Definition.Uid;
    chimeraBase["Description"] = chimeraData.Definition.Description;
    object val = null;
    switch (chimeraData)
    {
      case Chimera.Data.BaseGeometryData geoData:
      {
        chimeraBase[CHIMERA_VALUE_KEY] = converter.ConvertToSpeckle(geoData.TypedValue);
        break;
      }
      case Chimera.Data.SpeciesData sData:
      {
        foreach (IChimeraData childData in sData.Values)
        {
          chimeraBase[childData.Definition.Name] = ChimeraDataToSpeckle(childData, converter);
        }
        break;
      }
      case Chimera.Data.ListData lData:
      {
        for (int i = 0; i < lData.Count; i++)
        {
          chimeraBase[$"item_{i}"] = ChimeraDataToSpeckle(lData[i], converter);
        }
        break;
      }
      default:
        chimeraBase[CHIMERA_VALUE_KEY] = chimeraData.GetValue();
        break;  
    }
    return chimeraBase;
  }

  public static void ParseSpeckleToChimera(Base @object, IChimeraUserData cuo, ISpeckleConverter converter)
  {
    List<IChimeraData> receivedData = new();
    foreach (var propPair in @object.GetMembers())
    {
      if(string.Equals(propPair.Key, CHIMERA_OBJECT_ID_KEY, StringComparison.InvariantCultureIgnoreCase)) continue;
      if(propPair.Value is not Base speckObject) continue;
      receivedData.Add(ParseSpeckleToChimeraData(speckObject, converter));
    }
    cuo.BatchWriteData(receivedData);
  }

  static IChimeraData ParseSpeckleToChimeraData(Base speckleObject, ISpeckleConverter converter)
  {
    var attrId = Guid.Parse(speckleObject[CHIMERA_OBJECT_ID_KEY] as string ?? string.Empty);
    if (attrId == Guid.Empty) return null;
    var def = SpeciesManager.GetAttribute(attrId);
    var data = def.CreateDefaultData();
    var val = speckleObject[CHIMERA_VALUE_KEY];
    switch (data)
    {
      case Chimera.Data.BaseGeometryData geoData:
        if (val is not Base baseVal) break;
        data.SetValue(converter.ConvertToNative(baseVal));
        break;
      case Chimera.Data.ListData lData:
        {
          Dictionary<int,IChimeraData> listItems = new();
          foreach (var propPair in @speckleObject.GetMembers())
          {
            if(!propPair.Key.StartsWith("item_")) continue;
            listItems[int.Parse(propPair.Key.Substring(5))] =
              ParseSpeckleToChimeraData((Base)propPair.Value, converter);
          }
          foreach (var cData in listItems.OrderBy(x => x.Key).Select(x => x.Value))
          {
            lData.Add(cData);
          }
        }
        break;
      case Chimera.Data.SpeciesData sData:
        var sDef = def as Chimera.Definitions.SpeciesDefinition;
        foreach (var childDef in sDef.GetAttributes())
        {
          sData[childDef.Uid] = ParseSpeckleToChimeraData((Base)speckleObject[childDef.Name], converter);
        }
        break;
      default:
        data.SetValue(val);
        break;
    }
    return data;
  }
}
