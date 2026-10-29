namespace EDControls.EDLogo;

/// <summary>Jak se logo EDISON animuje.</summary>
public enum EDLogoAnimationMode
{
    /// <summary>Statické, rovnou složené logo.</summary>
    None,

    /// <summary>Krychle se jednou složí a zůstane složená (<see cref="EDLogo.AnimationCompleted"/>).</summary>
    Once,

    /// <summary>Složí se, drží, rozloží se a znovu - indikátor čekání.</summary>
    Loop,
}
