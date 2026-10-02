Include %occInclude

Class ALVA.EDISONMAIN2.Tests.SessionLockTest Extends ALVA.EDTesting.MLTestCase
{

/// Serverová semantika úklidu zámků session, na které stojí CompanySwitcher: ACTIVITY zámek
/// (per IdFa) i business zámky žijí v témže globálu ^CacheTempSYSLock a ALWA.SYS.Lock.LockClearExt
/// (logout shellu, SessionLogout) maže holdy GUIDu ve VŠECH objektech. Při přepnutí firmy GUID
/// přežije, takže logout staré firmy smete i ACTIVITY zámek nové - proto ho
/// LicenceVerifier.ReacquireLock zakládá znovu.
/// Mock testy v .NET tenhle předpoklad zachytit nemohou.

Parameter TESTIDFAA = "TEST_EDISON2_LOCKA";

Parameter TESTIDFAB = "TEST_EDISON2_LOCKB";

Parameter TESTIP = "10.255.255.254";

Parameter TESTGUID = "test-edison2-session-lock";

/// GUID jiné session - její zámek musí úklid TESTGUID přežít.
Parameter OTHERGUID = "test-edison2-session-lock-other";

Method KillTestLocks()
{
	do ##class(ALWA.SYS.Activity).LockDel(..#TESTIDFAA, ..#TESTIP, ..#TESTGUID)
	do ##class(ALWA.SYS.Activity).LockDel(..#TESTIDFAB, ..#TESTIP, ..#TESTGUID)
	do ##class(ALWA.SYS.Activity).LockDel(..#TESTIDFAA, ..#TESTIP, ..#OTHERGUID)
	kill ^CacheTempSYSLock("ACTIVITY/"_..#TESTIDFAA_"~"_..#TESTIP_"~"_..#TESTGUID)
	kill ^CacheTempSYSLock("ACTIVITY/"_..#TESTIDFAB_"~"_..#TESTIP_"~"_..#TESTGUID)
	kill ^CacheTempSYSLock("ACTIVITY/"_..#TESTIDFAA_"~"_..#TESTIP_"~"_..#OTHERGUID)
}

/// Úklid zámků session po drátě shellu (SessionLogout): object[] { guid } = ^||ML("IN",1).
/// Kořen ^||ML("IN") musí zůstat bez hodnoty: LockClearExt čte $i(^||ML("IN")) a s prázdným
/// guid smaže zámky celého serveru.
Method LockClearJakoShell(guid As %String)
{
	kill ^||ML
	do ..SetupSys(.sys)
	set ^||ML("IN",1) = guid
	set sc = ##class(ALWA.SYS.Lock).LockClearExt(.sys)
	do $$$AssertStatusOK(sc, "LockClearExt nesmí vrátit chybu")
	do $$$AssertEquals($get(^||ML("ERR")), "", "LockClearExt nesmí hlásit chybu v ^||ML(""ERR"")")
}

/// ACTIVITY zámek je v ^CacheTempSYSLock pod objektem "ACTIVITY/<idfa>~<ip>~<guid>"
/// a je zaevidovaný pod GUIDem session - tedy stejně jako business zámky.
Method TestActivityZamekJeVLockGlobaluPodGuidem()
{
	do ..KillTestLocks()
	set sc = ##class(ALWA.SYS.Activity).LockSet(..#TESTIDFAA, ..#TESTIP, ..#TESTGUID)
	do $$$AssertStatusOK(sc, "LockSet musí projít")

	set obj = "ACTIVITY/"_..#TESTIDFAA_"~"_..#TESTIP_"~"_..#TESTGUID
	do $$$AssertTrue($data(^CacheTempSYSLock(obj)), "ACTIVITY zámek patří do ^CacheTempSYSLock")
	do $$$AssertTrue($data(^CacheTempSYSLock(obj, "guid", ..#TESTGUID)), "zámek je zaevidovaný pod GUIDem session, sahá na něj i LockClearExt(guid)")

	do ..KillTestLocks()
}

/// Regrese: úklid logoutu (LockClearExt s guid jako ho posílá shell) je GUID-wide, ne per-IdFa -
/// smaže ACTIVITY zámky VŠECH firem téhož GUIDu, zámky jiného GUIDu nechá. Přepnutí firmy
/// proto nesmí spoléhat na to, že logout staré firmy zámek nové nechá naživu.
Method TestLockClearExtSmazeActivityZamkyVsechFirem()
{
	do ..KillTestLocks()
	do ##class(ALWA.SYS.Activity).LockSet(..#TESTIDFAA, ..#TESTIP, ..#TESTGUID)
	do ##class(ALWA.SYS.Activity).LockSet(..#TESTIDFAB, ..#TESTIP, ..#TESTGUID)
	do ##class(ALWA.SYS.Activity).LockSet(..#TESTIDFAA, ..#TESTIP, ..#OTHERGUID)
	do $$$AssertTrue(##class(ALWA.SYS.Activity).LockIs(..#TESTIDFAA, ..#TESTIP, ..#TESTGUID), "zámek firmy A existuje")
	do $$$AssertTrue(##class(ALWA.SYS.Activity).LockIs(..#TESTIDFAB, ..#TESTIP, ..#TESTGUID), "zámek firmy B existuje")
	do $$$AssertTrue(##class(ALWA.SYS.Activity).LockIs(..#TESTIDFAA, ..#TESTIP, ..#OTHERGUID), "zámek jiné session existuje")

	do ..LockClearJakoShell(..#TESTGUID)

	do $$$AssertNotTrue(##class(ALWA.SYS.Activity).LockIs(..#TESTIDFAA, ..#TESTIP, ..#TESTGUID), "LockClearExt smazal zámek firmy A")
	do $$$AssertNotTrue(##class(ALWA.SYS.Activity).LockIs(..#TESTIDFAB, ..#TESTIP, ..#TESTGUID), "LockClearExt smazal i zámek firmy B - proto se po logoutu staré firmy obnovuje zámek nové")
	do $$$AssertTrue(##class(ALWA.SYS.Activity).LockIs(..#TESTIDFAA, ..#TESTIP, ..#OTHERGUID), "zámek jiné session přežil - LockClearExt vzal guid z ^||ML(""IN"",1)")

	do ..KillTestLocks()
}

/// Premisa opravy: po GUID-wide úklidu jde zámek pod týmž GUIDem znovu založit.
Method TestZamekJdePoLockClearExtZnovuZalozit()
{
	do ..KillTestLocks()
	do ##class(ALWA.SYS.Activity).LockSet(..#TESTIDFAB, ..#TESTIP, ..#TESTGUID)
	do ..LockClearJakoShell(..#TESTGUID)
	do $$$AssertNotTrue(##class(ALWA.SYS.Activity).LockIs(..#TESTIDFAB, ..#TESTIP, ..#TESTGUID), "LockClearExt zámek smazal")

	set sc = ##class(ALWA.SYS.Activity).LockSet(..#TESTIDFAB, ..#TESTIP, ..#TESTGUID)
	do $$$AssertStatusOK(sc, "LockSet po LockClearExt musí projít")
	do $$$AssertTrue(##class(ALWA.SYS.Activity).LockIs(..#TESTIDFAB, ..#TESTIP, ..#TESTGUID), "zámek je zpátky")

	do ..KillTestLocks()
}

}
