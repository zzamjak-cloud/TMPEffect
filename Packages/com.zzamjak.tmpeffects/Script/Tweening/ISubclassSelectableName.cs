namespace CAT.UI
{
    /// <summary>
    /// <see cref="SubclassSelectorAttribute"/>가 적용된 필드의 드롭다운 메뉴에 클래스 이름 대신 표시될 이름을 제공합니다.
    /// </summary>
    public interface ISubclassSelectableName
    {
        string MenuName { get; }
    }
}
