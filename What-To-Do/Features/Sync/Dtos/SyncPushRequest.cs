namespace What_To_Do.Features.Sync.Dtos;
public class SyncPushRequest
{
    public IEnumerable<TodoSyncDto> Todos { get; set; } = Enumerable.Empty<TodoSyncDto>();
    public IEnumerable<NoteSyncDto> Notes { get; set; } = Enumerable.Empty<NoteSyncDto>();
    public IEnumerable<MeetingSyncDto> Meetings { get; set; } = Enumerable.Empty<MeetingSyncDto>();
}
