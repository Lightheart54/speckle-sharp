﻿using ConnectorGrasshopper.Extras;
using Grasshopper.Kernel;
using Speckle.Core.Api;
using Speckle.Core.Logging;
using Speckle.Core.Models;
using Speckle.Core.Transports;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;

namespace ConnectorGrasshopper.Transports
{
  public class SendReceiveTransport : GH_Component
  {
    public override Guid ComponentGuid { get => new Guid("4229B8DC-9F81-49A3-9EF9-DF3DE0B8E4B6"); }

    protected override Bitmap Icon => Properties.Resources.DiskTransport;

    public override GH_Exposure Exposure => GH_Exposure.primary;

    public SendReceiveTransport() : base("Send To Transports", "ST", "Sends an object to a list of given transports.", ComponentCategories.SECONDARY_RIBBON, ComponentCategories.TRANSPORTS) { }

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
      pManager.AddGenericParameter("transports", "T", "The transports to send to.", GH_ParamAccess.list);
      pManager.AddParameter(new SpeckleBaseParam("Object", "O", "The speckle object you want to send.", GH_ParamAccess.item));
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
      pManager.AddTextParameter("id", "ID", "The sent object's id.", GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess DA)
    {
      if (DA.Iteration != 0)
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "You can't send more than object. Please combine them into a root parent object using the create speckle object component!");
        return;
      }

      List<ITransport> transports = new List<ITransport>();
      DA.GetDataList(0, transports);

      GH_SpeckleBase obj = null;
      DA.GetData(1, ref obj);

      if (obj == null || obj.Value == null || transports.Count == 0)
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Invalid inputs.");
        return;
      }

      var res = Task.Run(async () => await Speckle.Core.Api.Operations.Send(obj.Value, transports, false)).Result;
      DA.SetData(0, res);
    }

    protected override void BeforeSolveInstance()
    {
      Tracker.TrackPageview("transports", "send_to_transport");
      base.BeforeSolveInstance();
    }

  }

  public class ReceiveFromTransport : GH_Component
  {
    public override Guid ComponentGuid { get => new Guid("8C7C6CA5-1557-4216-810B-F64E710526D0"); }

    protected override Bitmap Icon => Properties.Resources.DiskTransport;

    public override GH_Exposure Exposure => GH_Exposure.primary;

    public ReceiveFromTransport() : base("Receive From Transport", "RT", "Receives objects from a given transport.", ComponentCategories.SECONDARY_RIBBON, ComponentCategories.TRANSPORTS) { }

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
      pManager.AddGenericParameter("transport", "T", "The transport to receive from.", GH_ParamAccess.item);
      pManager.AddTextParameter("object ids", "IDs", "The ids of the objects you want to receive.", GH_ParamAccess.list);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
      pManager.AddGenericParameter("objects", "O", "The objects you requested.", GH_ParamAccess.list);
    }

    protected override void SolveInstance(IGH_DataAccess DA)
    {
      if (DA.Iteration != 0)
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "TODO: Error message.");
        return;
      }

      List<string> ids = new List<string>();
      DA.GetDataList(1, ids);

      object transportGoo = null;
      DA.GetData(0, ref transportGoo);

      var transport = transportGoo.GetType().GetProperty("Value").GetValue(transportGoo) as ITransport;

      List<Base> results = new List<Base>();
      foreach(var id in ids)
      {
        var res = Task.Run(async () => await Operations.Receive(id, null, transport)).Result;
        results.Add(res);
      }

      DA.SetDataList(0, results);
    }

    protected override void BeforeSolveInstance()
    {
      Tracker.TrackPageview("transports", "receive_from_transport");
      base.BeforeSolveInstance();
    }

  }
}
