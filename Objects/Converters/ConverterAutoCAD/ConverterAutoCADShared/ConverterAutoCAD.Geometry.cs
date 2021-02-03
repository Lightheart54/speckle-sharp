﻿using Objects.Geometry;
using Objects.Primitive;
using Speckle.Core.Models;
using Speckle.Core.Kits;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autodesk.AutoCAD.Geometry;
using Arc = Objects.Geometry.Arc;
using Circle = Objects.Geometry.Circle;
using ControlPoint = Objects.Geometry.ControlPoint;
using Curve = Objects.Geometry.Curve;
using Ellipse = Objects.Geometry.Ellipse;
using Interval = Objects.Primitive.Interval;
using Line = Objects.Geometry.Line;
using Plane = Objects.Geometry.Plane;
using Point = Objects.Geometry.Point;
using Polyline = Objects.Geometry.Polyline;
using Vector = Objects.Geometry.Vector;
using AC = Autodesk.AutoCAD.Geometry;

namespace Objects.Converter.AutoCAD
{
  public partial class ConverterAutoCAD
  {
    // tolerance for geometry:
    public double tolerance = 0.00;

    // Convenience methods point:
    public double[] PointToArray(Point3d pt)
    {
      return new double[] { pt.X, pt.Y, pt.Z };
    }

    public double[] PointToArray(Point2d pt)
    {
      return new double[] { pt.X, pt.Y };
    }

    // Mass point converter
    public Point3d[] PointListToNative(IEnumerable<double> arr, string units)
    {
      var enumerable = arr.ToList();
      if (enumerable.Count % 3 != 0) throw new Exception("Array malformed: length%3 != 0.");

      Point3d[] points = new Point3d[enumerable.Count / 3];
      var asArray = enumerable.ToArray();
      for (int i = 2, k = 0; i < enumerable.Count; i += 3)
        points[k++] = new Point3d(
          ScaleToNative(asArray[i - 2], units),
          ScaleToNative(asArray[i - 1], units),
          ScaleToNative(asArray[i], units));

      return points;
    }

    public double[] PointsToFlatArray(IEnumerable<Point3d> points)
    {
      return points.SelectMany(pt => PointToArray(pt)).ToArray();
    }

    // Convenience methods vector:
    public double[] VectorToArray(Vector3d vc)
    {
      return new double[] { vc.X, vc.Y, vc.Z };
    }

    public Vector3d ArrayToVector(double[] arr)
    {
      return new Vector3d(arr[0], arr[1], arr[2]);
    }

    // Points
    public Point PointToSpeckle(Point3d pt)
    {
      return new Point(pt.X, pt.Y, pt.Z, ModelUnits);
    }
    public Point3d PointToNative(Point pt)
    {
      var nativePt = new Point3d(ScaleToNative(pt.value[0], pt.units),
        ScaleToNative(pt.value[1], pt.units),
        ScaleToNative(pt.value[2], pt.units));
      return nativePt;
    }

    public List<List<ControlPoint>> ControlPointsToSpeckle(Point3dCollection points, DoubleCollection weights)
    {
      var _weights = new List<double>();
      var _points = new List<Point3d>();
      foreach (var point in points)
        _points.Add((Point3d)point);
      foreach (var weight in weights)
        _weights.Add((double)weight);

      var controlPoints = new List<List<ControlPoint>>();
      /* TODO: Figure out how collections are structured (do we lose UV info?)
      for (int i = 0; i < _points.Count; i++)
      {
        controlPoints.Add(new ControlPoint(_points[i].X, _points[i].Y, _points[i].Z, _weights[i], ModelUnits));
      }
      */
      return controlPoints;
    }

    // Vectors
    public Vector VectorToSpeckle(Vector3d pt)
    {
      return new Vector(pt.X, pt.Y, pt.Z, ModelUnits);
    }
    public Vector3d VectorToNative(Vector pt)
    {
      return new Vector3d(
        ScaleToNative(pt.value[0], pt.units),
        ScaleToNative(pt.value[1], pt.units),
        ScaleToNative(pt.value[2], pt.units));
    }

    // Interval
    public Interval IntervalToSpeckle(AC.Interval interval)
    {
      var speckleInterval = new Interval(interval.LowerBound, interval.UpperBound);
      return speckleInterval;
    }
    public AC.Interval IntervalToNative(Interval interval)
    {
      return new AC.Interval((double)interval.start, (double)interval.end, tolerance);
    }

    // Plane
    public Plane PlaneToSpeckle(AC.Plane plane)
    {
      Vector xAxis = VectorToSpeckle(plane.GetCoordinateSystem().Xaxis);
      Vector yAxis = VectorToSpeckle(plane.GetCoordinateSystem().Yaxis);
      Point origin = PointToSpeckle(plane.GetCoordinateSystem().Origin); // this may be the plane origin, not sure
      return new Plane(PointToSpeckle(plane.PointOnPlane), VectorToSpeckle(plane.Normal), xAxis,
        yAxis, ModelUnits);
    }
    public AC.Plane PlaneToNative(Plane plane)
    {
      return new AC.Plane(PointToNative(plane.origin), VectorToNative(plane.normal));
    }

    // Line
    public Line LineToSpeckle(Line3d line)
    {
      return new Line(PointsToFlatArray(new Point3d[] { line.StartPoint, line.EndPoint }), ModelUnits);
    }
    public Line LineToSpeckle(LineSegment3d line)
    {
      return new Line(PointsToFlatArray(new Point3d[] { line.StartPoint, line.EndPoint }), ModelUnits);
    }
    public Line3d LineToNative(Line line)
    {
      var pts = PointListToNative(line.value, line.units);
      var _line = new Line3d(pts[0], pts[1]);
      if (line.domain != null)
        _line.SetInterval(IntervalToNative(line.domain));
      return _line;
    }

    // Arc
    public Arc ArcToSpeckle(CircularArc3d arc)
    {
      var _arc = new Arc(PlaneToSpeckle(arc.GetPlane()), arc.Radius, arc.StartAngle, arc.EndAngle, Math.Abs(arc.EndAngle - arc.StartAngle), ModelUnits);
      _arc.startPoint = PointToSpeckle(arc.StartPoint);
      _arc.endPoint = PointToSpeckle(arc.EndPoint);
      _arc.domain = IntervalToSpeckle(arc.GetInterval());
      return _arc;
    }

    public PolylineCurve3d PolylineToNative(Polyline polyline)
    {
      var points = PointListToNative(polyline.value, polyline.units).ToList();
      if (polyline.closed) points.Add(points[0]);
      var _polyline = new PolylineCurve3d(new Point3dCollection(points.ToArray()));
      if (polyline.domain != null)
        _polyline.SetInterval(IntervalToNative(polyline.domain));
      return _polyline;
    }

    // Curve
    public Curve3d CurveToNative(ICurve curve)
    {
      switch (curve)
      {
        case Circle circle:
          return null;

        case Arc arc:
          return null;

        case Ellipse ellipse:
          return null;

        case Curve crv:
          return NurbsToNative(crv);

        case Polyline polyline:
          return PolylineToNative(polyline);

        case Line line:
          return LineToNative(line);

        case Polycurve polycurve:
          return null;

        default:
          return null;
      }
    }

    public ICurve CurveToSpeckle(Curve3d curve)
    {
      if (curve.IsPlanar(out AC.Plane pln))
      {
        if (curve.IsPeriodic(out double period) && curve.IsClosed())
        {
        }

        if (curve.IsLinear(out Line3d line)) // defaults to polyline
        {
          if (null != line)
          {
            return LineToSpeckle(line);
          }
        }
      }

      return NurbsToSpeckle(curve as NurbCurve3d);
    }

    public Curve NurbsToSpeckle(NurbCurve3d curve)
    {
      return null;
    }

    public NurbCurve3d NurbsToNative(Curve curve)
    {
      var ptsList = PointListToNative(curve.points, curve.units);

      IntPtr newUnmanaged = new IntPtr(); // check this!!
      var nurbsCurve = NurbCurve3d.Create(newUnmanaged, true);

      for (int j = 0; j < ptsList.Length; j++)
      {
        nurbsCurve.SetFitPointAt(j, ptsList[j]);
        nurbsCurve.SetWeightAt(j, curve.weights[j]);
      }

      for (int j = 0; j < nurbsCurve.Knots.Count; j++)
      {
        nurbsCurve.Knots[j] = curve.knots[j];
      }

      nurbsCurve.SetInterval(IntervalToNative(curve.domain ?? new Interval(0, 1)));
      return nurbsCurve;
    }

    public AC.NurbSurface SurfaceToNative(Geometry.Surface surface)
    {
      // Create ac surface
      var points = surface.GetControlPoints().Select(l => l.Select(p =>
        new ControlPoint(
          ScaleToNative(p.x, p.units),
          ScaleToNative(p.y, p.units),
          ScaleToNative(p.z, p.units),
          p.weight,
          p.units)).ToList()).ToList();

      var result = AC.NurbSurface.Create(new IntPtr(), true); // check what new unmanaged pointer does!!

      // Get control points
      Point3dCollection controlPoints = new Point3dCollection();
      DoubleCollection weights = new DoubleCollection();
      for (var i = 0; i < points.Count; i++)
      {
        for (var j = 0; j < points[i].Count; j++)
        {
          var pt = points[i][j];
          controlPoints.Add(PointToNative(pt));
          weights.Add(pt.weight);
        }
      }

      // Get knot vectors
      KnotCollection UKnots = new KnotCollection();
      KnotCollection VKnots = new KnotCollection();
      for (int i = 0; i < surface.knotsU.Count; i++)
      {
        UKnots.Add(surface.knotsU[i]);
      }

      for (int i = 0; i < surface.knotsV.Count; i++)
      {
        VKnots.Add(surface.knotsV[i]);
      }

      // Set surface info
      result.Set(surface.degreeU, surface.degreeV, 0, 0, surface.countU, surface.countV, controlPoints, weights, UKnots, VKnots);

      // Return surface
      return result;
    }

    public Geometry.Surface SurfaceToSpeckle(AC.NurbSurface surface)
    {
      List<double> Uknots = new List<double>();
      List<double> Vknots = new List<double>();
      foreach (var knot in surface.UKnots)
      {
        Uknots.Add((double)knot);
      }
      foreach (var knot in surface.VKnots)
      {
        Vknots.Add((double)knot);
      }
      var result = new Geometry.Surface
      {
        degreeU = surface.DegreeInU,
        degreeV = surface.DegreeInV,
        rational = surface.IsRationalInU && surface.IsRationalInV,
        closedU = surface.IsClosedInU(),
        closedV = surface.IsClosedInV(),
        domainU = IntervalToSpeckle(surface.GetEnvelope()[0]),
        domainV = IntervalToSpeckle(surface.GetEnvelope()[1]),
        knotsU = Uknots,
        knotsV = Vknots
      };
      result.units = ModelUnits;
      result.SetControlPoints(ControlPointsToSpeckle(surface.ControlPoints, surface.Weights));
      return result;
    }
  }
}