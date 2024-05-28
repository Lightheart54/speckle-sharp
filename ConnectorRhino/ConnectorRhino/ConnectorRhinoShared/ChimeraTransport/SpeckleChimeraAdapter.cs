using System;
using System.Collections.Generic;
using System.Linq;
using Chimera;
using Chimera.Data;
using Chimera.DataAccess;
using Chimera.Definitions;
using JetBrains.Annotations;
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

  [CanBeNull]
  static object ChimeraDataToSpeckle(IChimeraData chimeraData, ISpeckleConverter converter) =>
    chimeraData switch
    {
      Chimera.Data.BaseGeometryData geoData => converter.ConvertToSpeckle(geoData.TypedValue),
      Chimera.Data.SpeciesData sData => ChimeraSpeciesToSpeckle(sData, converter),
      Chimera.Data.ListData lData => ChimeraListToSpeckle(lData, converter),
      _ => chimeraData.GetValue()
    };

  static Base ChimeraSpeciesToSpeckle(Chimera.Data.SpeciesData speciesData, ISpeckleConverter converter)
  {
    var chimeraBase = new Base();
    chimeraBase[CHIMERA_OBJECT_ID_KEY] = speciesData.Key.First.Key.ToString();
    chimeraBase["Description"] = speciesData.Definition.Description;
    foreach (IChimeraData data in speciesData.Values)
    {
      chimeraBase[data.Definition.Name] = ChimeraDataToSpeckle(data, converter);
    }

    return chimeraBase;
  }
  
  static Base ChimeraListToSpeckle(Chimera.Data.ListData listData, ISpeckleConverter converter)
  {
    var chimeraBase = new Base();
    for (int i = 0; i < listData.Count; i++)
    {
      var data = listData[i];
      chimeraBase[$"{data.Definition.Name}_[{i}]"] = ChimeraDataToSpeckle(data, converter);
    }
    return chimeraBase;
  }

  public static void ParseSpeckleToChimera(Base @object, IChimeraUserData cuo, ISpeckleConverter converter)
  {
    List<IChimeraData> receivedData = new();
    foreach (var propPair in @object.GetMembers())
    {
      if(string.Equals(propPair.Key, CHIMERA_OBJECT_ID_KEY, StringComparison.InvariantCultureIgnoreCase)) continue;
      if(propPair.Value is not Base dataObject) continue;
      var defIdObject = dataObject[CHIMERA_OBJECT_ID_KEY];
      var attrId = Guid.Parse(dataObject[CHIMERA_OBJECT_ID_KEY] as string ?? string.Empty);
      if (attrId == Guid.Empty) continue;
      var def = SpeciesManager.GetAttribute(attrId);
      if (def is not SpeciesDefinition sDef) continue;
      var sData = sDef.CreateDefaultData() as SpeciesData;
      ParseSpeckleToChimeraSpecies(sData, dataObject, converter);
      
      receivedData.Add(sData);
    }
    cuo.BatchWriteData(receivedData);
  }

  static void ParseSpeckleToChimeraData(IChimeraData data, object speckleData, ISpeckleConverter converter)
  {
    switch (data)
    {
      case SpeciesData sData:
        ParseSpeckleToChimeraSpecies(sData,speckleData as Base, converter);
        break;
      case ListData lData:
        ParseSpeckleToChimeraList(lData, speckleData as Base, converter);
        break;
      case BaseGeometryData geoData:
        geoData.SetValue(converter.ConvertToNative(speckleData as Base));
        break;
      default:
        data.SetValue(speckleData);
        break;
    }
  }

  static void ParseSpeckleToChimeraSpecies(SpeciesData sData, Base speckleObject, ISpeckleConverter converter)
  {
    foreach (var defChild in sData.Values)
    {
      ParseSpeckleToChimeraData(defChild, speckleObject[defChild.Definition.Name], converter);
    }
  }

  public static void ParseSpeckleToChimeraList(ListData lData, Base speckleObject, ISpeckleConverter converter)
  {
    var speckleData = speckleObject.GetMembers();
    #nullable enable
    SortedDictionary<int, IChimeraData> parsedData = new();
    foreach (KeyValuePair<string,object?> valuePair in speckleData)
    {
      if(!valuePair.Key.StartsWith(lData.Definition.Name)) continue;
      //get the index
      var indexStart = valuePair.Key.LastIndexOf('[');
      var indexEnd = valuePair.Key.LastIndexOf(']');
      if(indexStart == -1 || indexEnd == -1 || indexStart >= indexEnd) continue;
      
      var indexStr = valuePair.Key.Substring(indexStart + 1, indexEnd - indexStart - 1);
      if (!int.TryParse(indexStr, out int index)) continue;
      
      var itemData = lData.GetDefaultItem();
      ParseSpeckleToChimeraData(itemData, valuePair.Value, converter);
      parsedData[index] = itemData;
    }
    //add the items in order to the data
    foreach (var kvp in parsedData)
    {
      lData.Add(kvp.Value);
    }
  }
}
