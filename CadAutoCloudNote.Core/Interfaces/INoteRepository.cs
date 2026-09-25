using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using CadAutoCloudNote.Core.Models;

namespace CadAutoCloudNote.Core.Interfaces
{
    /// <summary>
    /// 批注仓储接口（定义图纸级批注存取契约）
    /// </summary>
    public interface INoteRepository
    {
        /// <summary>
        /// 获取当前图纸内所有有效批注
        /// </summary>
        List<NoteRecord> GetAllNotes(Database db);

        /// <summary>
        /// 根据 NoteId 获取单条批注
        /// </summary>
        NoteRecord GetNote(Database db, string noteId);

        /// <summary>
        /// 保存或更新批注（写入 XRecord 4+2 分片）
        /// </summary>
        bool SaveNote(Database db, NoteRecord note);

        /// <summary>
        /// 删除批注（标记删除或同步移除 XRecord）
        /// </summary>
        bool DeleteNote(Database db, string noteId);

        /// <summary>
        /// 获取图纸内批注总数
        /// </summary>
        int GetNoteCount(Database db);
    }
}
