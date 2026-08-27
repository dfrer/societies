using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Societies.Core
{
    /// <summary>Non-authoritative mesh/collision projection for construction and its current ghost.</summary>
    public partial class VoxelWorldcraftPresenter : Node3D
    {
        private readonly Dictionary<string, PieceProjection> _pieces = new();
        private Node3D? _ghost;

        public bool GhostValid { get; private set; }
        public bool HasGhost => _ghost != null && IsInstanceValid(_ghost) && !_ghost.IsQueuedForDeletion();

        public void SetActive(bool active)
        {
            Visible = active;
            HideGhost();
            foreach (CollisionShape3D shape in GetChildren().OfType<StaticBody3D>().SelectMany(body => body.GetChildren().OfType<CollisionShape3D>()))
                shape.Disabled = !active;
        }

        public void ApplyPieces(IEnumerable<WorldcraftPieceSnapshot> pieces)
        {
            WorldcraftPieceSnapshot[] desired = pieces.Select(piece => new WorldcraftPieceSnapshot
            {
                InstanceId = piece.InstanceId, PieceId = piece.PieceId, Anchor = piece.Anchor,
                RotationQuarterTurns = piece.RotationQuarterTurns, PlacedTick = piece.PlacedTick
            }).ToArray();
            Dictionary<string, WorldcraftPieceSnapshot> byId = desired.ToDictionary(piece => piece.InstanceId, StringComparer.Ordinal);
            foreach (string stale in _pieces.Keys.Where(id => !byId.TryGetValue(id, out WorldcraftPieceSnapshot? piece) || !_pieces[id].Matches(piece)).ToArray())
            {
                _pieces[stale].Body.QueueFree();
                _pieces.Remove(stale);
            }
            foreach (WorldcraftPieceSnapshot piece in desired)
            {
                if (_pieces.ContainsKey(piece.InstanceId)) continue;
                WorldcraftPieceDefinition? definition = VoxelWorldcraftCatalog.FindPiece(piece.PieceId); if (definition == null) continue;
                StaticBody3D root = new() { Name = $"Worldcraft_{piece.InstanceId}" };
                root.SetMeta("worldcraft_instance_id", piece.InstanceId);
                AddPieceShape(root, piece.PieceId, piece.Anchor, piece.RotationQuarterTurns, new Color("80522f"), 1.0f, collision: true);
                AddChild(root); _pieces.Add(piece.InstanceId, new PieceProjection(root, piece.PieceId, piece.Anchor, piece.RotationQuarterTurns));
            }
        }

        public bool HasExactPieceProjection(WorldcraftPieceSnapshot piece) =>
            _pieces.TryGetValue(piece.InstanceId, out PieceProjection? projection) && projection != null && projection.Matches(piece);

        public void ShowGhost(WorldcraftPlacementEvaluation evaluation)
        {
            HideGhost();
            if (evaluation.Definition == null || evaluation.Cells.Count == 0) return;
            _ghost = new Node3D { Name = "WorldcraftPreview" }; GhostValid = evaluation.IsValid;
            Color color = GhostValid ? new Color(0.29f, 0.70f, 0.42f, 0.42f) : new Color(0.78f, 0.22f, 0.16f, 0.45f);
            AddPieceShape(_ghost, evaluation.Definition.Id, evaluation.Anchor, evaluation.RotationQuarterTurns, color, color.A, collision: false);
            AddChild(_ghost);
        }

        public void HideGhost()
        {
            if (_ghost != null && IsInstanceValid(_ghost)) _ghost.QueueFree();
            _ghost = null;
            GhostValid = false;
        }

        public static bool TryResolvePieceInstance(GodotObject? collider, out string instanceId)
        {
            Node? node = collider as Node;
            while (node != null)
            {
                if (node.HasMeta("worldcraft_instance_id"))
                {
                    instanceId = node.GetMeta("worldcraft_instance_id").AsString();
                    return !string.IsNullOrWhiteSpace(instanceId);
                }
                node = node.GetParent();
            }
            instanceId = string.Empty;
            return false;
        }

        private static void AddPieceShape(Node3D parent, string pieceId, VoxelCoord anchor, int rotation,
            Color color, float alpha, bool collision)
        {
            (Vector3 center, Vector3 size) = Shape(pieceId, anchor, rotation);
            StandardMaterial3D material = new()
            {
                AlbedoColor = new Color(color, alpha),
                Roughness = 0.86f,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel,
                Transparency = alpha < 1.0f ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
                NoDepthTest = alpha < 1.0f
            };
            MeshInstance3D mesh = new()
            {
                Name = $"{pieceId}_mesh",
                Mesh = new BoxMesh { Size = size },
                Position = center,
                MaterialOverride = material,
                CastShadow = alpha < 1.0f ? GeometryInstance3D.ShadowCastingSetting.Off : GeometryInstance3D.ShadowCastingSetting.On
            };
            parent.AddChild(mesh);
            if (collision) parent.AddChild(new CollisionShape3D { Name = $"{pieceId}_collision", Shape = new BoxShape3D { Size = size }, Position = center });
        }

        private sealed record PieceProjection(StaticBody3D Body, string PieceId, VoxelCoord Anchor, int RotationQuarterTurns)
        {
            public bool Matches(WorldcraftPieceSnapshot piece) => PieceId == piece.PieceId && Anchor == piece.Anchor &&
                RotationQuarterTurns == piece.RotationQuarterTurns && IsInstanceValid(Body) && !Body.IsQueuedForDeletion();
        }

        public static (Vector3 Center, Vector3 Size) Shape(string pieceId, VoxelCoord anchor, int rotation)
        {
            bool quarterTurn = rotation % 2 != 0;
            return pieceId switch
            {
                "wood_floor" => (new Vector3(anchor.X + (quarterTurn ? 0.5f : (rotation == 2 ? 0.0f : 1.0f)), anchor.Y + 0.09f,
                    anchor.Z + (quarterTurn ? (rotation == 3 ? 0.0f : 1.0f) : 0.5f)), quarterTurn ? new Vector3(1.0f, 0.18f, 2.0f) : new Vector3(2.0f, 0.18f, 1.0f)),
                "wood_wall" => (new Vector3(anchor.X + 0.5f, anchor.Y + 1.0f, anchor.Z + 0.5f),
                    quarterTurn ? new Vector3(0.16f, 2.0f, 1.0f) : new Vector3(1.0f, 2.0f, 0.16f)),
                "wood_post" => (new Vector3(anchor.X + 0.5f, anchor.Y + 1.5f, anchor.Z + 0.5f), new Vector3(0.28f, 3.0f, 0.28f)),
                _ => throw new System.ArgumentOutOfRangeException(nameof(pieceId))
            };
        }

    }
}
