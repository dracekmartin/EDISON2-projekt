Include %occInclude

Class ALVA.EDISONMAIN2.Tests.SessionLockTest Extends ALVA.EDTesting.MLTestCase
{

/// Serverová semantika úklidu zámků session, na které stojí CompanySwitcher: ACTIVITY zámek
/// (per IdFa) i business zámky žijí v témže globálu ^CacheTempSYSLock a ALWA.SYS.Lock.LockClear
/// maže holdy GUIDu ve VŠECH objektech. Při přepnutí firmy GUID přežije, takže logout staré firmy
/// smete i ACTIVITY zámek nové - proto ho LicenceVerifier.ReacquireLock zakládá znovu.
/// Mock testy v .NET tenhle předpoklad zachytit nemohou.

Parameter TESTIDFAA = "TEST_EDISON2_LOCKA";

Parameter TESTIDFAB = "TEST_EDISON2_LOCKB";

Parameter TESTIP = "10.255.255.254";

Parameter TESTGUID = "test-edison2-session-lock";

Method KillTestLocks()
{
	do ##class(ALWA.SYS.Activity).LockDel(..#TESTIDFAA, ..#TESTIP, ..#TESTGUID)
	do ##class(ALWA.SYS.Activity).LockDel(..#TESTIDFAB, ..#TESTIP, ..#TESTGUID)
	kill ^CacheTempSYSLock("ACTIVITY/"_..#TESTIDFAA_"~"_..#TESTIP_"~"_..#TESTGUID)
	kill ^CacheTempSYSLock("ACTIVITY/"_..#TESTIDFAB_"~"_..#TESTIP_"~"_..#TESTGUID)
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
	do $$$AssertTrue($data(^CacheTempSYSLock(obj, "guid", ..#TESTGUID)), "zámek je zaevidovaný pod GUIDem session, sahá na něj i LockClear(guid)")

	do ..KillTestLocks()
}

/// Regrese: LockClear(guid) je GUID-wide, ne per-IdFa - smaže ACTIVITY zámky VŠECH firem
/// téhož GUIDu. Přepnutí firmy proto nesmí spoléhat na to, že logout staré firmy
/// zámek nové nechá naživu.
Method TestLockClearSmazeActivityZamkyVsechFirem()
{
	do ..KillTestLocks()
	do ##class(ALWA.SYS.Activity).LockSet(..#TESTIDFAA, ..#TESTIP, ..#TESTGUID)
	do ##class(ALWA.SYS.Activity).LockSet(..#TESTIDFAB, ..#TESTIP, ..#TESTGUID)
	do $$$AssertTrue(##class(ALWA.SYS.Activity).LockIs(..#TESTIDFAA, ..#TESTIP, ..#TESTGUID), "zámek firmy A existuje")
	do $$$AssertTrue(##class(ALWA.SYS.Activity).LockIs(..#TESTIDFAB, ..#TESTIP, ..#TESTGUID), "zámek firmy B existuje")

	do ##class(ALWA.SYS.Lock).LockClear(..#TESTGUID)

	do $$$AssertNotTrue(##class(ALWA.SYS.Activity).LockIs(..#TESTIDFAA, ..#TESTIP, ..#TESTGUID), "LockClear smazal zámek firmy A")
	do $$$AssertNotTrue(##class(ALWA.SYS.Activity).LockIs(..#TESTIDFAB, ..#TESTIP, ..#TESTGUID), "LockClear smazal i zámek firmy B - proto se po logoutu staré firmy obnovuje zámek nové")

	do ..KillTestLocks()
}

/// Premisa opravy: po GUID-wide úklidu jde zámek pod týmž GUIDem znovu založit.
Method TestZamekJdePoLockClearZnovuZalozit()
{
	do ..KillTestLocks()
	do ##class(ALWA.SYS.Activity).LockSet(..#TESTIDFAB, ..#TESTIP, ..#TESTGUID)
	do ##class(ALWA.SYS.Lock).LockClear(..#TESTGUID)

	set sc = ##class(ALWA.SYS.Activity).LockSet(..#TESTIDFAB, ..#TESTIP, ..#TESTGUID)
	do $$$AssertStatusOK(sc, "LockSet po LockClear musí projít")
	do $$$AssertTrue(##class(ALWA.SYS.Activity).LockIs(..#TESTIDFAB, ..#TESTIP, ..#TESTGUID), "zámek je zpátky")

	do ..KillTestLocks()
}

}
