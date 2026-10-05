namespace IFP4;

public interface IAttendanceStore
{
    void Append(IEnumerable<AttendanceEntry> entries);
    IEnumerable<AttendanceEntry> Load();
}
