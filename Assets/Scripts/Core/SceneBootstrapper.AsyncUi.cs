using System;
using Sandplay.UI;
using UnityEngine;

namespace Sandplay.Core
{
    public partial class SceneBootstrapper
    {
        private const float DefaultUiOperationTimeoutSeconds = 20f;
        private BlockingOperationFeedback _blockingOperationFeedback;

        private BlockingOperationFeedback.Ticket BeginUiOperation(
            string message = null,
            float timeoutSeconds = DefaultUiOperationTimeoutSeconds,
            Action onTimeout = null)
        {
            if (_safeArea == null) return null;
            if (_blockingOperationFeedback == null)
            {
                _blockingOperationFeedback = _safeArea.GetComponent<BlockingOperationFeedback>();
                if (_blockingOperationFeedback == null)
                    _blockingOperationFeedback = _safeArea.gameObject.AddComponent<BlockingOperationFeedback>();
            }

            _blockingOperationFeedback.Configure(
                GetUIFont(),
                new Color(0f, 0f, 0f, HomeIsLight ? .08f : .18f),
                HomeCard,
                HomeCardBorder,
                HomePrimary,
                HomeText,
                image => ApplyHomeRoundedCorners(image, 12f));

            return _blockingOperationFeedback.Begin(
                message ?? F("Loading…", "正在加载…"),
                timeoutSeconds,
                onTimeout ?? (() => ShowLockedFeatureDialog(
                    F("This is taking longer than expected. Please try again.", "处理时间过长，请重试。"),
                    offerUpgrade: false)));
        }

        private static bool CompleteUiOperation(BlockingOperationFeedback.Ticket ticket)
            => ticket == null || ticket.TryComplete();
    }
}
