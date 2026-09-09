using System;

namespace RevitSyncService.Core.Services
{
    /// <summary>
    /// Строка-идентификатор текущей машины/пользователя для блокировок объектов.
    /// Используется одинаково и для ВПК, и для БПК — по ней в БД и в логах видно,
    /// кто именно занял объект.
    /// </summary>
    public static class MachineIdentity
    {
        public static string Current => $"{Environment.MachineName} / {Environment.UserName}";
    }
}