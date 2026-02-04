using System;

namespace Server
{
    public class Proces
    {
        public string Naziv { get; set; }
        public int VrijemeIzvrsavanja { get; set; }
        public int Prioritet { get; set; }
        public double ZauzeceProcessora { get; set; }
        public double ZauzeceMemorije { get; set; }

        
        public int OriginalnoVrijemeIzvrsavanja { get; set; }

        public Proces()
        {
        }

        public Proces(string naziv, int vrijemeIzvrsavanja, int prioritet,
                     double zauzeceProcessora, double zauzeceMemorije)
        {
            Naziv = naziv;
            VrijemeIzvrsavanja = vrijemeIzvrsavanja;
            OriginalnoVrijemeIzvrsavanja = vrijemeIzvrsavanja;
            Prioritet = prioritet;
            ZauzeceProcessora = zauzeceProcessora;
            ZauzeceMemorije = zauzeceMemorije;
        }

        public override string ToString()
        {
            return $"Naziv: {Naziv,-15} | Vrijeme: {VrijemeIzvrsavanja,3}s | " +
                   $"Prioritet: {Prioritet} | CPU: {ZauzeceProcessora,5}% | " +
                   $"Memorija: {ZauzeceMemorije,5}%";
        }
    }
}