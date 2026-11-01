namespace ClipFlow.Core.Interfaces;

public interface IStartupManager
{
    bool IsRunAtStartupEnabled();
    bool SetRunAtStartup(bool enable);
}
