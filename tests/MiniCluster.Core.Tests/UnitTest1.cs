namespace MiniCluster.Core.Tests;

public class FakeSensor : ISensor
{
    private readonly bool _state;

    public FakeSensor(bool state)
    {
        _state = state;
    }

    public bool IsActive()
    {
        return _state;
    }
}

public class DoorControllerTests
{
    [Fact]
    public void CanOpen_센서가_켜져있을때_참을_반환한다()
    {
        // 1. 준비
        var fakeSensor = new FakeSensor(false);
        var controller = new DoorController(fakeSensor);
        // 2. 실행
        bool result = controller.CanOpen();
        // 3. 검증
        Assert.True(result);
    }
}