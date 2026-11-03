/// Prázdná náhrada ukázkové agendy Faktury, smazané 30. 10. (`B56` S11). Na REL i u zákazníků zůstala z dřívějších
/// vydání stará třída, která dědí ze základu agend a volá jeho metody, které už neexistují (`WriteTemplate2Col`):
/// import nového základu ji přeloží a import i instalace na ní skončí (REL 3. 11.). Tahle verze ji přepíše.
/// Agendou není (nedědí z `BaseAgenda`), registr ji proto nenabídne. Smazat, až ji budou mít všichni zákazníci.
Class ALVA.TemplateDesigner.Agendy.Faktury Extends %RegisteredObject
{

}
