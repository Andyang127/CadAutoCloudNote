using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace CadAutoCloudNote.Core.Reactors
{
    /// <summary>
    /// 单文档内部操作深度与事件上下文状态机
    /// </summary>
    public class DocumentState
    {
        private int _internalDepth = 0;
        public Database TargetDatabase { get; private set; }
        public List<PendingTask> PendingTasks { get; } = new List<PendingTask>();
        public bool IsCommandUndoing { get; set; }
        public string ActiveCommand { get; set; }

        public DocumentState(Database db)
        {
            TargetDatabase = db;
        }

        public bool IsInternalOperation => _internalDepth > 0;

        public IDisposable EnterInternalScope()
        {
            _internalDepth++;
            return new ScopeExit(() =>
            {
                if (_internalDepth > 0)
                    _internalDepth--;
            });
        }

        private class ScopeExit : IDisposable
        {
            private readonly Action _onDispose;
            private bool _disposed = false;

            public ScopeExit(Action onDispose)
            {
                _onDispose = onDispose;
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    _disposed = true;
                    _onDispose?.Invoke();
                }
            }
        }
    }
}
