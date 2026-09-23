using System.Collections.Generic;

namespace Sandplay.Core
{
    public partial class NetworkBootstrapper
    {
        private readonly HashSet<uint> _loadingObjects = new HashSet<uint>();
        // Moves are absolute transforms; only the latest transform is needed while a model loads.
        private readonly Dictionary<uint, byte[]> _loadingMoves = new Dictionary<uint, byte[]>();
        private readonly Dictionary<uint, object> _modelLoads = new Dictionary<uint, object>();
        private readonly HashSet<uint> _snapshotObjectsPending = new HashSet<uint>();
        private int _snapshotObjectTotal;
        private int _snapshotObjectResolved;
        private bool _snapshotObjectFailed;
        private string _snapshotFailureReason;
        private bool _snapshotTerminalNotified;

        private void ResetSnapshotObjectProgress()
        {
            _snapshotObjectsPending.Clear();
            _snapshotObjectTotal = 0;
            _snapshotObjectResolved = 0;
            _snapshotObjectFailed = false;
            _snapshotFailureReason = null;
            _snapshotTerminalNotified = false;
            OnSnapshotLoadProgress?.Invoke(0, 0);
        }

        private void BeginSnapshotObjectProgress(SpawnObjectData[] objects)
        {
            _snapshotObjectsPending.Clear();
            if (objects != null)
                foreach (var item in objects)
                    _snapshotObjectsPending.Add(item.NetId);
            _snapshotObjectTotal = _snapshotObjectsPending.Count;
            _snapshotObjectResolved = 0;
            _snapshotObjectFailed = false;
            _snapshotFailureReason = null;
            _snapshotTerminalNotified = false;
            OnSnapshotLoadProgress?.Invoke(0, _snapshotObjectTotal);
        }

        private void ResolveSnapshotObject(uint id, bool succeeded, string failureReason = null)
        {
            if (!_snapshotObjectsPending.Remove(id)) return;
            _snapshotObjectResolved++;
            if (!succeeded)
            {
                _snapshotObjectFailed = true;
                if (string.IsNullOrEmpty(_snapshotFailureReason))
                    _snapshotFailureReason = failureReason;
            }
            OnSnapshotLoadProgress?.Invoke(_snapshotObjectResolved, _snapshotObjectTotal);
        }

        private object BeginModelLoad(uint id)
        {
            var ticket = new object();
            _modelLoads[id] = ticket;
            _loadingObjects.Add(id);
            return ticket;
        }

        private bool IsCurrentModelLoad(uint id, object ticket)
        {
            return _loadingObjects.Contains(id) && _modelLoads.TryGetValue(id, out var current)
                && ReferenceEquals(current, ticket);
        }

        private void EndModelLoad(uint id)
        {
            _loadingObjects.Remove(id);
            _modelLoads.Remove(id);
        }

        private void ResetPendingModels()
        {
            _modelLoads.Clear();
            _loadingObjects.Clear();
            _loadingMoves.Clear();
        }
    }
}
