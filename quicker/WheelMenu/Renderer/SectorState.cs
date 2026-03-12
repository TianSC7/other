namespace WheelMenu.Renderer;

public enum SectorState
{
    Empty,    // 无动作绑定，显示 "+"
    Normal,   // 有动作，未高亮
    Hovered   // 鼠标悬停高亮
}

public enum DisplayMode
{
    IconAndLabel,  // 有图标 + 有文字（默认）
    IconOnly,      // 有图标时隐藏文字
    LabelOnly      // 无图标，只显文字
}
