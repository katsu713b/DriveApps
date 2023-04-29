using DriveApp.GPSLapTimer.Core.Gps;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace DriveApp.GPSLapTimer.Core.Circuits
{
    internal interface ICircuit { }
    internal class Circuit : ICircuit
    {
        public string Name { get; }
        public Segment ControlLine { get; }
        public Segment Sector1Line { get; }
        public Segment Sector2Line { get; }

        public IEnumerable<(GeoPoint P1, GeoPoint P2)> GetAreaPoints()
        {
            for (int i = 0; i < _areaPoints.Length; i++)
            {
                if (i == _areaPoints.Length - 1)
                {
                    yield return (_areaPoints[i], _areaPoints[0]);
                }
                else
                {
                    yield return (_areaPoints[i], _areaPoints[i + 1]);
                }
            }
        }

        private GeoPoint[] _areaPoints;

        public Circuit(string name, Segment control, Segment s1, Segment s2, GeoPoint[] areaPoints)
        {
            Name = name;
            ControlLine = control;
            Sector1Line = s1;
            Sector2Line = s2;
            _areaPoints = areaPoints;
        }

        public bool PassControlLine(Segment car) => CrossSegments(ControlLine, car);
        public bool PassSec1Line(Segment car) => CrossSegments(Sector1Line, car);
        public bool PassSec2Line(Segment car) => CrossSegments(Sector2Line, car);

        private bool CrossSegments(Segment seg1, Segment seg2)
        {

            /*
            // 線分構造体
            struct Segment {
               D3DXVECTOR2 s; // 始点
               D3DXVECTOR2 v; // 方向ベクトル（線分の長さも担うので正規化しないように！）
            };

            // 2Dベクトルの外積
            float D3DXVec2Cross( D3DXVECTOR2* v1, D3DXVECTOR2* v2 ) {
               return v1->x * v2->y - v1->y * v2->x;
            }

            // 線分の衝突
            bool ColSegments(
               Segment &seg1,          // 線分1
               Segment &seg2,          // 線分2
               float* outT1 = 0,       // 線分1の内分比（出力）
               float* outT2 = 0,       // 線分2の内分比（出力
               D3DXVECTOR2* outPos = 0 // 交点（出力）
            ) {

               D3DXVECTOR2 v = seg2.s - seg1.s;
               float Crs_v1_v2 = D3DXVec2Cross( &seg1.v, &seg2.v );
               if ( Crs_v1_v2 == 0.0f ) {
                  // 平行状態
                  return false;
               }

               float Crs_v_v1 = D3DXVec2Cross( &v, &seg1.v );
               float Crs_v_v2 = D3DXVec2Cross( &v, &seg2.v );

               float t1 = Crs_v_v2 / Crs_v1_v2;
               float t2 = Crs_v_v1 / Crs_v1_v2;

               if ( outT1 )
                  *outT1 = Crs_v_v2 / Crs_v1_v2;
               if ( outT2 )
                  *outT2 = Crs_v_v1 / Crs_v1_v2;

               const float eps = 0.00001f;
               if ( t1 + eps < 0 || t1 - eps > 1 || t2 + eps < 0 || t2 - eps > 1 ) {
                  // 交差していない
                  return false;
               }

               if( outPos )
                  *outPos = seg1.s + seg1.v * t1;

               return true;
            }
            */


            var crs12 = Vector3.Cross(seg1.Vector, seg2.Vector).Z;
            if (crs12 == 0) return false; // 平行状態

            var v = seg1.StartPoint.CreateV3FromGeo(seg2.StartPoint);
            var crsv1 = Vector3.Cross(v, seg1.Vector).Z;
            var crsv2 = Vector3.Cross(v, seg2.Vector).Z;

            var t1 = crsv2 / crs12;
            var t2 = crsv1 / crs12;

            const float eps = 0.00001f;
            if (t1 + eps < 0 || t1 - eps > 1 || t2 + eps < 0 || t2 - eps > 1)
            {
                // 交差していない
                return false;
            }

            return true;
        }
    }

    internal static class CircuitLocation
    {
        private static readonly Lazy<IEnumerable<Circuit>> _lazyLines = new Lazy<IEnumerable<Circuit>>(() =>
        {
            
            return App.Config.Locations.Select(l =>
                new Circuit(
                    l.Name,
                    new Segment(new GeoPoint(l.ControlLine.Start[0], l.ControlLine.Start[1]), new GeoPoint(l.ControlLine.End[0], l.ControlLine.End[1])),
                    new Segment(new GeoPoint(l.Sector1Line.Start[0], l.Sector1Line.Start[1]), new GeoPoint(l.Sector1Line.End[0], l.Sector1Line.End[1])),
                    new Segment(new GeoPoint(l.Sector2Line.Start[0], l.Sector2Line.Start[1]), new GeoPoint(l.Sector2Line.End[0], l.Sector2Line.End[1])),
                    l.AreaPoints.Select(p => new GeoPoint(p[0], p[1])).ToArray()
                    )
                );
        });

        public static IEnumerable<Circuit> AllCircuit => _lazyLines.Value;
    }
}
