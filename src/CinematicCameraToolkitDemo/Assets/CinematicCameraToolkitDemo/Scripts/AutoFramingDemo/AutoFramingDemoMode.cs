namespace CinematicCameraToolkitDemo.AutoFraming
{
    /// <summary>
    /// デモのキャラクター駆動モード。3 つのデモで共通の定義を使う。
    /// </summary>
    public enum AutoFramingDemoMode
    {
        /// <summary>
        /// 離散的なポーズを切り替える。スムージングは常に OFF。
        /// </summary>
        Pose,

        /// <summary>
        /// クリップを連続再生する。スムージングは切り替え可能。
        /// </summary>
        Animation,
    }
}
