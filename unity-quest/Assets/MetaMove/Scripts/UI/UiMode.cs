namespace MetaMove.UI
{
    // Three overall presentation modes the user can switch between via radial / config.
    //   Minimal       — default. Radial launches single mini-panels on demand.
    //   ControlCenter — 3 persistent panels arranged in a spatial triangle around user
    //                   (left/front/right). No open/close cycling; everything visible.
    //   FlexPendant   — ABB-style wrist pendant. Small tablet attached to non-dominant
    //                   wrist with jog + program + E-Stop. Flip-wrist to view.
    public enum UiMode { Minimal, ControlCenter, FlexPendant }
}
