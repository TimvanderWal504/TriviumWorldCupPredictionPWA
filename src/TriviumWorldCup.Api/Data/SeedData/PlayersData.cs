using TriviumWorldCup.Api.Domain;

namespace TriviumWorldCup.Api.Data.SeedData;

/// <summary>
/// Representative player rosters for all 48 teams.
/// Aim: ~15-20 players per team covering GK/DEF/MID/FWD positions.
/// Used primarily by the Golden Six prediction feature (TWC-7/8).
///
/// Data is based on best available training data (up to Aug 2025).
/// Players whose exact position is uncertain are marked // TODO: verify position.
/// Club affiliations are not stored — only national team, name, and position.
/// Shirt numbers are omitted (not confirmed pre-tournament for most squads).
/// </summary>
public static class PlayersData
{
    public static IReadOnlyList<Player> All => _all;

    private static Player P(string teamId, string name, Position pos)
        => new() { Id = Guid.NewGuid(), TeamId = teamId, Name = name, Position = pos };

    private static readonly List<Player> _all =
    [
        // ════════════════════════════════════════════════════════════════════
        // GROUP A
        // ════════════════════════════════════════════════════════════════════

        // USA
        P("USA", "Matt Turner",         Position.GK),
        P("USA", "Zack Steffen",        Position.GK),
        P("USA", "Sergino Dest",        Position.DEF),
        P("USA", "Joe Scally",          Position.DEF),
        P("USA", "Tim Ream",            Position.DEF),
        P("USA", "Miles Robinson",      Position.DEF),
        P("USA", "Chris Richards",      Position.DEF),
        P("USA", "Antonee Robinson",    Position.DEF),
        P("USA", "Tyler Adams",         Position.MID),
        P("USA", "Weston McKennie",     Position.MID),
        P("USA", "Yunus Musah",         Position.MID),
        P("USA", "Gio Reyna",           Position.MID),
        P("USA", "Christian Pulisic",   Position.FWD),
        P("USA", "Folarin Balogun",     Position.FWD),
        P("USA", "Josh Sargent",        Position.FWD),
        P("USA", "Ricardo Pepi",        Position.FWD),
        P("USA", "Brenden Aaronson",    Position.MID),

        // URU
        P("URU", "Sergio Rochet",       Position.GK),
        P("URU", "Fernando Muslera",    Position.GK),
        P("URU", "Ronald Araújo",       Position.DEF),
        P("URU", "Mathías Olivera",     Position.DEF),
        P("URU", "José María Giménez",  Position.DEF),
        P("URU", "Sebastián Cáceres",   Position.DEF),
        P("URU", "Guillermo Varela",    Position.DEF),
        P("URU", "Federico Valverde",   Position.MID),
        P("URU", "Rodrigo Bentancur",   Position.MID),
        P("URU", "Manuel Ugarte",       Position.MID),
        P("URU", "Nicolás de la Cruz",  Position.MID),
        P("URU", "Darwin Núñez",        Position.FWD),
        P("URU", "Luis Suárez",         Position.FWD),
        P("URU", "Facundo Torres",      Position.FWD),
        P("URU", "Maximiliano Araújo",  Position.FWD),

        // PAN
        P("PAN", "Luis Mejía",          Position.GK),
        P("PAN", "Orlando Mosquera",    Position.GK),
        P("PAN", "Éric Davis",          Position.DEF),
        P("PAN", "Fidel Escobar",       Position.DEF),
        P("PAN", "Harold Cummings",     Position.DEF),
        P("PAN", "Andrés Andrade",      Position.DEF),
        P("PAN", "Adalberto Carrasquilla", Position.MID),
        P("PAN", "Édgar Bárcenas",      Position.MID),
        P("PAN", "César Yanis",         Position.MID),
        P("PAN", "Aníbal Godoy",        Position.MID),
        P("PAN", "Ismael Díaz",         Position.FWD),
        P("PAN", "Rolando Blackburn",   Position.FWD),
        P("PAN", "Cecilio Waterman",    Position.FWD),
        P("PAN", "Alfredo Stephens",    Position.FWD),
        P("PAN", "Michael Murillo",     Position.DEF),

        // BOL
        P("BOL", "Carlos Lampe",        Position.GK),
        P("BOL", "Guillermo Viscarra",  Position.GK),
        P("BOL", "Diego Bejarano",      Position.DEF),
        P("BOL", "Luis Haquin",         Position.DEF),
        P("BOL", "José Sagredo",        Position.DEF),
        P("BOL", "Adrián Jusino",       Position.DEF),
        P("BOL", "Ramiro Vaca",         Position.MID),
        P("BOL", "Fernando Saucedo",    Position.MID),
        P("BOL", "Erwin Saavedra",      Position.MID),
        P("BOL", "Rodrigo Ramallo",     Position.FWD),
        P("BOL", "Marcelo Martins",     Position.FWD),
        P("BOL", "Henry Vaca",          Position.FWD),
        P("BOL", "Boris Sagredo",       Position.DEF), // TODO: verify position
        P("BOL", "Diego Wayar",         Position.MID),
        P("BOL", "Jeyson Chura",        Position.FWD), // TODO: verify

        // ════════════════════════════════════════════════════════════════════
        // GROUP B
        // ════════════════════════════════════════════════════════════════════

        // MEX
        P("MEX", "Guillermo Ochoa",     Position.GK),
        P("MEX", "Alfredo Talavera",    Position.GK),
        P("MEX", "Héctor Moreno",       Position.DEF),
        P("MEX", "César Montes",        Position.DEF),
        P("MEX", "Jorge Sánchez",       Position.DEF),
        P("MEX", "Gerardo Arteaga",     Position.DEF),
        P("MEX", "Jesús Gallardo",      Position.DEF),
        P("MEX", "Edson Álvarez",       Position.MID),
        P("MEX", "Carlos Rodríguez",    Position.MID),
        P("MEX", "Héctor Herrera",      Position.MID),
        P("MEX", "Roberto Alvarado",    Position.MID),
        P("MEX", "Hirving Lozano",      Position.FWD),
        P("MEX", "Raúl Jiménez",        Position.FWD),
        P("MEX", "Alexis Vega",         Position.FWD),
        P("MEX", "Uriel Antuna",        Position.FWD),
        P("MEX", "Henry Martín",        Position.FWD),
        P("MEX", "Santiago Giménez",    Position.FWD),

        // ARG
        P("ARG", "Emiliano Martínez",   Position.GK),
        P("ARG", "Franco Armani",       Position.GK),
        P("ARG", "Cristian Romero",     Position.DEF),
        P("ARG", "Lisandro Martínez",   Position.DEF),
        P("ARG", "Nicolás Otamendi",    Position.DEF),
        P("ARG", "Nahuel Molina",       Position.DEF),
        P("ARG", "Marcos Acuña",        Position.DEF),
        P("ARG", "Rodrigo De Paul",     Position.MID),
        P("ARG", "Alexis Mac Allister", Position.MID),
        P("ARG", "Enzo Fernández",      Position.MID),
        P("ARG", "Leandro Paredes",     Position.MID),
        P("ARG", "Lionel Messi",        Position.FWD),
        P("ARG", "Julián Álvarez",      Position.FWD),
        P("ARG", "Ángel Di María",      Position.FWD),
        P("ARG", "Lautaro Martínez",    Position.FWD),
        P("ARG", "Paulo Dybala",        Position.FWD),

        // NZL
        P("NZL", "Michael Woud",        Position.GK),
        P("NZL", "Stefan Marinovic",    Position.GK),
        P("NZL", "Michael Boxall",      Position.DEF),
        P("NZL", "Winston Reid",        Position.DEF),
        P("NZL", "Liberato Cacace",     Position.DEF),
        P("NZL", "Nando Pijnaker",      Position.DEF),
        P("NZL", "Bill Tuiloma",        Position.DEF),
        P("NZL", "Clayton Lewis",       Position.MID),
        P("NZL", "Elijah Just",         Position.MID),
        P("NZL", "Sarpreet Singh",      Position.MID),
        P("NZL", "Tim Payne",           Position.MID), // TODO: verify
        P("NZL", "Chris Wood",          Position.FWD),
        P("NZL", "Hamish Watson",       Position.FWD), // TODO: verify
        P("NZL", "Marko Stamenic",      Position.MID),
        P("NZL", "Ben Waine",           Position.FWD),

        // JAM
        P("JAM", "Andre Blake",         Position.GK),
        P("JAM", "Dillon Barnes",       Position.GK),
        P("JAM", "Adrian Mariappa",     Position.DEF),
        P("JAM", "Damion Lowe",         Position.DEF),
        P("JAM", "Kemar Lawrence",      Position.DEF),
        P("JAM", "Alex Marshall",       Position.DEF), // TODO: verify
        P("JAM", "Javain Brown",        Position.DEF),
        P("JAM", "Daniel Johnson",      Position.MID),
        P("JAM", "Ravel Morrison",      Position.MID),
        P("JAM", "Je-Vaughn Watson",    Position.MID),
        P("JAM", "Fabian McCarthy",     Position.MID), // TODO: verify
        P("JAM", "Michail Antonio",     Position.FWD),
        P("JAM", "Bobby Reid",          Position.FWD),
        P("JAM", "Shamar Nicholson",    Position.FWD),
        P("JAM", "Leon Bailey",         Position.FWD),

        // ════════════════════════════════════════════════════════════════════
        // GROUP C
        // ════════════════════════════════════════════════════════════════════

        // CAN
        P("CAN", "Maxime Crépeau",      Position.GK),
        P("CAN", "Milan Borjan",        Position.GK),
        P("CAN", "Kamal Miller",        Position.DEF),
        P("CAN", "Steven Vitória",      Position.DEF),
        P("CAN", "Alistair Johnston",   Position.DEF),
        P("CAN", "Sam Adekugbe",        Position.DEF),
        P("CAN", "Derek Cornelius",     Position.DEF),
        P("CAN", "Atiba Hutchinson",    Position.MID),
        P("CAN", "Stephen Eustáquio",   Position.MID),
        P("CAN", "Liam Fraser",         Position.MID),
        P("CAN", "Ismaël Koné",         Position.MID),
        P("CAN", "Alphonso Davies",     Position.DEF), // plays LB/LW
        P("CAN", "Jonathan David",      Position.FWD),
        P("CAN", "Cyle Larin",          Position.FWD),
        P("CAN", "Tajon Buchanan",      Position.FWD),
        P("CAN", "Junior Hoilett",      Position.FWD),

        // MAR
        P("MAR", "Yassine Bounou",      Position.GK),
        P("MAR", "Munir Mohamedi",      Position.GK),
        P("MAR", "Nayef Aguerd",        Position.DEF),
        P("MAR", "Romain Saïss",        Position.DEF),
        P("MAR", "Noussair Mazraoui",   Position.DEF),
        P("MAR", "Achraf Hakimi",       Position.DEF),
        P("MAR", "Jawad El Yamiq",      Position.DEF),
        P("MAR", "Sofyan Amrabat",      Position.MID),
        P("MAR", "Azzedine Ounahi",     Position.MID),
        P("MAR", "Selim Amallah",       Position.MID),
        P("MAR", "Bilal El Khannous",   Position.MID),
        P("MAR", "Hakim Ziyech",        Position.FWD),
        P("MAR", "Youssef En-Nesyri",   Position.FWD),
        P("MAR", "Sofiane Boufal",      Position.FWD),
        P("MAR", "Abde Ezzalzouli",     Position.FWD),

        // ECU
        P("ECU", "Hernán Galíndez",     Position.GK),
        P("ECU", "Alexander Domínguez", Position.GK),
        P("ECU", "Byron Castillo",      Position.DEF),
        P("ECU", "Piero Hincapié",      Position.DEF),
        P("ECU", "Robert Arboleda",     Position.DEF),
        P("ECU", "Félix Torres",        Position.DEF),
        P("ECU", "Ángelo Preciado",     Position.DEF),
        P("ECU", "Moisés Caicedo",      Position.MID),
        P("ECU", "Carlos Gruezo",       Position.MID),
        P("ECU", "Jeremy Sarmiento",    Position.MID),
        P("ECU", "Jhegson Méndez",      Position.MID),
        P("ECU", "Enner Valencia",      Position.FWD),
        P("ECU", "Michael Estrada",     Position.FWD),
        P("ECU", "Kevin Rodríguez",     Position.FWD),
        P("ECU", "Gonzalo Plata",       Position.FWD),

        // TUN
        P("TUN", "Aymen Dahmen",        Position.GK),
        P("TUN", "Mouez Hassen",        Position.GK),
        P("TUN", "Montassar Talbi",     Position.DEF),
        P("TUN", "Dylan Bronn",         Position.DEF),
        P("TUN", "Ali Maaloul",         Position.DEF),
        P("TUN", "Wajdi Kechrida",      Position.DEF),
        P("TUN", "Aïssa Laïdouni",      Position.MID),
        P("TUN", "Ellyes Skhiri",       Position.MID),
        P("TUN", "Ferjani Sassi",       Position.MID),
        P("TUN", "Hannibal Mejbri",     Position.MID),
        P("TUN", "Youssef Msakni",      Position.FWD),
        P("TUN", "Naim Sliti",          Position.FWD),
        P("TUN", "Seifeddine Jaziri",   Position.FWD),
        P("TUN", "Issam Jebali",        Position.FWD),
        P("TUN", "Hamza Rafia",         Position.MID),

        // ════════════════════════════════════════════════════════════════════
        // GROUP D
        // ════════════════════════════════════════════════════════════════════

        // BRA
        P("BRA", "Alisson",             Position.GK),
        P("BRA", "Ederson",             Position.GK),
        P("BRA", "Marquinhos",          Position.DEF),
        P("BRA", "Éder Militão",        Position.DEF),
        P("BRA", "Danilo",              Position.DEF),
        P("BRA", "Alex Sandro",         Position.DEF),
        P("BRA", "Gabriel Magalhães",   Position.DEF),
        P("BRA", "Casemiro",            Position.MID),
        P("BRA", "Bruno Guimarães",     Position.MID),
        P("BRA", "Lucas Paquetá",       Position.MID),
        P("BRA", "Gerson",              Position.MID),
        P("BRA", "Vinícius Júnior",     Position.FWD),
        P("BRA", "Rodrygo",             Position.FWD),
        P("BRA", "Raphinha",            Position.FWD),
        P("BRA", "Gabriel Jesus",       Position.FWD),
        P("BRA", "Neymar",              Position.FWD), // TODO: verify fitness/squad inclusion
        P("BRA", "Endrick",             Position.FWD),

        // GER
        P("GER", "Manuel Neuer",        Position.GK),
        P("GER", "Marc-André ter Stegen", Position.GK),
        P("GER", "Antonio Rüdiger",     Position.DEF),
        P("GER", "Matthias Ginter",     Position.DEF),
        P("GER", "David Raum",          Position.DEF),
        P("GER", "Thilo Kehrer",        Position.DEF),
        P("GER", "Nico Schlotterbeck",  Position.DEF),
        P("GER", "Joshua Kimmich",      Position.MID),
        P("GER", "Leon Goretzka",       Position.MID),
        P("GER", "İlkay Gündoğan",      Position.MID),
        P("GER", "Jamal Musiala",       Position.MID),
        P("GER", "Thomas Müller",       Position.FWD),
        P("GER", "Leroy Sané",          Position.FWD),
        P("GER", "Serge Gnabry",        Position.FWD),
        P("GER", "Florian Wirtz",       Position.MID),
        P("GER", "Kai Havertz",         Position.FWD),
        P("GER", "Niclas Füllkrug",     Position.FWD),

        // JPN
        P("JPN", "Shuichi Gonda",       Position.GK),
        P("JPN", "Zion Suzuki",         Position.GK),
        P("JPN", "Hiroki Sakai",        Position.DEF),
        P("JPN", "Ko Itakura",          Position.DEF),
        P("JPN", "Takehiro Tomiyasu",   Position.DEF),
        P("JPN", "Yuto Nagatomo",       Position.DEF),
        P("JPN", "Shogo Taniguchi",     Position.DEF),
        P("JPN", "Wataru Endō",         Position.MID),
        P("JPN", "Takumi Minamino",     Position.MID),
        P("JPN", "Gaku Shibasaki",      Position.MID), // TODO: verify squad inclusion
        P("JPN", "Kaoru Mitoma",        Position.FWD),
        P("JPN", "Ritsu Doan",          Position.FWD),
        P("JPN", "Daichi Kamada",       Position.MID),
        P("JPN", "Kyogo Furuhashi",     Position.FWD),
        P("JPN", "Ayase Ueda",          Position.FWD),
        P("JPN", "Junya Ito",           Position.FWD),

        // CMR
        P("CMR", "André Onana",         Position.GK),
        P("CMR", "Devis Epassy",        Position.GK),
        P("CMR", "Nicolas Nkoulou",     Position.DEF),
        P("CMR", "Ambroise Oyongo",     Position.DEF),
        P("CMR", "Harold Moukoudi",     Position.DEF),
        P("CMR", "Collins Fai",         Position.DEF),
        P("CMR", "Michael Ngadeu-Ngadjui", Position.DEF),
        P("CMR", "André Zambo Anguissa", Position.MID),
        P("CMR", "Samuel Gouet",        Position.MID),
        P("CMR", "Olivier Ntcham",      Position.MID),
        P("CMR", "Bryan Mbeumo",        Position.FWD),
        P("CMR", "Vincent Aboubakar",   Position.FWD),
        P("CMR", "Eric Maxim Choupo-Moting", Position.FWD),
        P("CMR", "Karl Toko Ekambi",    Position.FWD),
        P("CMR", "Christian Bassogog",  Position.FWD),

        // ════════════════════════════════════════════════════════════════════
        // GROUP E
        // ════════════════════════════════════════════════════════════════════

        // ESP
        P("ESP", "David Raya",          Position.GK),
        P("ESP", "Unai Simón",          Position.GK),
        P("ESP", "Dani Carvajal",       Position.DEF),
        P("ESP", "Pau Cubarsí",         Position.DEF),
        P("ESP", "Robin Le Normand",    Position.DEF),
        P("ESP", "Alejandro Grimaldo",  Position.DEF),
        P("ESP", "Marc Cucurella",      Position.DEF),
        P("ESP", "Rodri",               Position.MID),
        P("ESP", "Pedri",               Position.MID),
        P("ESP", "Fabián Ruiz",         Position.MID),
        P("ESP", "Gavi",                Position.MID),
        P("ESP", "Lamine Yamal",        Position.FWD),
        P("ESP", "Nico Williams",       Position.FWD),
        P("ESP", "Álvaro Morata",       Position.FWD),
        P("ESP", "Mikel Oyarzabal",     Position.FWD),
        P("ESP", "Dani Olmo",           Position.MID),

        // SRB
        P("SRB", "Vanja Milinković-Savić", Position.GK),
        P("SRB", "Predrag Rajković",    Position.GK),
        P("SRB", "Stefan Mitrović",     Position.DEF),
        P("SRB", "Nikola Milenković",   Position.DEF),
        P("SRB", "Srđan Babić",         Position.DEF),
        P("SRB", "Strahinja Pavlović",   Position.DEF),
        P("SRB", "Aleksandar Kolarov",  Position.DEF), // TODO: verify squad inclusion at age
        P("SRB", "Nemanja Maksimović",   Position.MID),
        P("SRB", "Sergej Milinković-Savić", Position.MID),
        P("SRB", "Nemanja Gudelj",      Position.MID),
        P("SRB", "Filip Kostić",        Position.MID),
        P("SRB", "Aleksandar Mitrović", Position.FWD),
        P("SRB", "Dušan Vlahović",      Position.FWD),
        P("SRB", "Luka Jović",          Position.FWD),
        P("SRB", "Andrija Živković",    Position.FWD),

        // COL
        P("COL", "Camilo Vargas",       Position.GK),
        P("COL", "David Ospina",        Position.GK),
        P("COL", "Yerry Mina",          Position.DEF),
        P("COL", "Dávinson Sánchez",    Position.DEF),
        P("COL", "Johan Mojica",        Position.DEF),
        P("COL", "Daniel Muñoz",        Position.DEF),
        P("COL", "Santiago Arias",      Position.DEF),
        P("COL", "Wilmar Barrios",      Position.MID),
        P("COL", "Juan Guillermo Cuadrado", Position.MID),
        P("COL", "Mateus Uribe",        Position.MID),
        P("COL", "Jhon Arias",          Position.MID),
        P("COL", "Luis Díaz",           Position.FWD),
        P("COL", "Radamel Falcao",      Position.FWD), // TODO: verify squad inclusion at age
        P("COL", "Rafael Santos Borré", Position.FWD),
        P("COL", "James Rodríguez",     Position.MID),
        P("COL", "Miguel Borja",        Position.FWD),

        // NGR
        P("NGR", "Francis Uzoho",       Position.GK),
        P("NGR", "Maduka Okoye",        Position.GK),
        P("NGR", "William Troost-Ekong", Position.DEF),
        P("NGR", "Semi Ajayi",          Position.DEF),
        P("NGR", "Zaidu Sanusi",        Position.DEF),
        P("NGR", "Bright Osayi-Samuel", Position.DEF),
        P("NGR", "Ola Aina",            Position.DEF),
        P("NGR", "Wilfred Ndidi",       Position.MID),
        P("NGR", "Joe Aribo",           Position.MID),
        P("NGR", "Alex Iwobi",          Position.MID),
        P("NGR", "Kelechi Iheanacho",   Position.FWD),
        P("NGR", "Victor Osimhen",      Position.FWD),
        P("NGR", "Samuel Chukwueze",    Position.FWD),
        P("NGR", "Moses Simon",         Position.FWD),
        P("NGR", "Terem Moffi",         Position.FWD),

        // ════════════════════════════════════════════════════════════════════
        // GROUP F
        // ════════════════════════════════════════════════════════════════════

        // POR
        P("POR", "Rui Patrício",        Position.GK),
        P("POR", "Diogo Costa",         Position.GK),
        P("POR", "João Cancelo",        Position.DEF),
        P("POR", "Rúben Dias",          Position.DEF),
        P("POR", "Pepe",                Position.DEF), // TODO: verify squad inclusion at age
        P("POR", "Nuno Mendes",         Position.DEF),
        P("POR", "Danilo Pereira",      Position.DEF),
        P("POR", "João Palhinha",       Position.MID),
        P("POR", "Vitinha",             Position.MID),
        P("POR", "Bernardo Silva",      Position.MID),
        P("POR", "Bruno Fernandes",     Position.MID),
        P("POR", "Cristiano Ronaldo",   Position.FWD),
        P("POR", "Diogo Jota",          Position.FWD),
        P("POR", "Rafael Leão",         Position.FWD),
        P("POR", "João Félix",          Position.FWD),
        P("POR", "Gonçalo Ramos",       Position.FWD),

        // CRO
        P("CRO", "Dominik Livaković",   Position.GK),
        P("CRO", "Ivica Ivušić",        Position.GK),
        P("CRO", "Dejan Lovren",        Position.DEF),
        P("CRO", "Joško Gvardiol",      Position.DEF),
        P("CRO", "Borna Sosa",          Position.DEF),
        P("CRO", "Josip Juranović",     Position.DEF),
        P("CRO", "Domagoj Vida",        Position.DEF),
        P("CRO", "Luka Modrić",         Position.MID),
        P("CRO", "Marcelo Brozović",    Position.MID),
        P("CRO", "Mateo Kovačić",       Position.MID),
        P("CRO", "Nikola Vlašić",       Position.MID),
        P("CRO", "Ivan Perišić",        Position.FWD),
        P("CRO", "Bruno Petković",      Position.FWD),
        P("CRO", "Andrej Kramarić",     Position.FWD),
        P("CRO", "Marko Livaja",        Position.FWD),

        // SEN
        P("SEN", "Édouard Mendy",       Position.GK),
        P("SEN", "Alfred Gomis",        Position.GK),
        P("SEN", "Kalidou Koulibaly",   Position.DEF),
        P("SEN", "Abdou Diallo",        Position.DEF),
        P("SEN", "Saliou Ciss",         Position.DEF),
        P("SEN", "Youssouf Sabaly",     Position.DEF),
        P("SEN", "Formose Mendy",       Position.DEF),
        P("SEN", "Cheikhou Kouyaté",    Position.MID),
        P("SEN", "Pape Matar Sarr",     Position.MID),
        P("SEN", "Nampalys Mendy",      Position.MID),
        P("SEN", "Sadio Mané",          Position.FWD),
        P("SEN", "Ismaïla Sarr",        Position.FWD),
        P("SEN", "Boulaye Dia",         Position.FWD),
        P("SEN", "Nicolas Jackson",     Position.FWD),
        P("SEN", "Habib Diallo",        Position.FWD),

        // IRI
        P("IRI", "Alireza Beiranvand",  Position.GK),
        P("IRI", "Hossein Hosseini",    Position.GK),
        P("IRI", "Morteza Pouraliganji", Position.DEF),
        P("IRI", "Majid Hosseini",      Position.DEF),
        P("IRI", "Ehsan Hajsafi",       Position.DEF),
        P("IRI", "Shoja Khalilzadeh",   Position.DEF),
        P("IRI", "Mohammad Mohammadi",  Position.DEF),
        P("IRI", "Saeid Ezatolahi",     Position.MID),
        P("IRI", "Ahmad Noorollahi",    Position.MID),
        P("IRI", "Ali Gholizadeh",      Position.MID),
        P("IRI", "Sardar Azmoun",       Position.FWD),
        P("IRI", "Mehdi Taremi",        Position.FWD),
        P("IRI", "Allahyar Sayyadmanesh", Position.FWD),
        P("IRI", "Karim Ansarifard",    Position.FWD), // TODO: verify squad inclusion
        P("IRI", "Roozbeh Cheshmi",    Position.DEF),

        // ════════════════════════════════════════════════════════════════════
        // GROUP G
        // ════════════════════════════════════════════════════════════════════

        // ENG
        P("ENG", "Jordan Pickford",     Position.GK),
        P("ENG", "Nick Pope",           Position.GK),
        P("ENG", "Kyle Walker",         Position.DEF),
        P("ENG", "Harry Maguire",       Position.DEF),
        P("ENG", "John Stones",         Position.DEF),
        P("ENG", "Luke Shaw",           Position.DEF),
        P("ENG", "Kieran Trippier",     Position.DEF),
        P("ENG", "Declan Rice",         Position.MID),
        P("ENG", "Jude Bellingham",     Position.MID),
        P("ENG", "Conor Gallagher",     Position.MID),
        P("ENG", "Phil Foden",          Position.MID),
        P("ENG", "Harry Kane",          Position.FWD),
        P("ENG", "Marcus Rashford",     Position.FWD),
        P("ENG", "Bukayo Saka",         Position.FWD),
        P("ENG", "Jack Grealish",       Position.FWD),
        P("ENG", "Raheem Sterling",     Position.FWD),
        P("ENG", "Cole Palmer",         Position.MID),

        // FRA
        P("FRA", "Hugo Lloris",         Position.GK),
        P("FRA", "Mike Maignan",        Position.GK),
        P("FRA", "Raphaël Varane",      Position.DEF),
        P("FRA", "Presnel Kimpembe",    Position.DEF), // TODO: verify fitness
        P("FRA", "Benjamin Pavard",     Position.DEF),
        P("FRA", "Theo Hernandez",      Position.DEF),
        P("FRA", "Jules Koundé",        Position.DEF),
        P("FRA", "N'Golo Kanté",        Position.MID),
        P("FRA", "Aurélien Tchouaméni", Position.MID),
        P("FRA", "Adrien Rabiot",       Position.MID),
        P("FRA", "Antoine Griezmann",   Position.FWD),
        P("FRA", "Kylian Mbappé",       Position.FWD),
        P("FRA", "Olivier Giroud",      Position.FWD),
        P("FRA", "Ousmane Dembélé",     Position.FWD),
        P("FRA", "Marcus Thuram",       Position.FWD),
        P("FRA", "Eduardo Camavinga",   Position.MID),

        // AUS
        P("AUS", "Mat Ryan",            Position.GK),
        P("AUS", "Danny Vukovic",       Position.GK),
        P("AUS", "Harry Souttar",       Position.DEF),
        P("AUS", "Miloš Degenek",       Position.DEF),
        P("AUS", "Aziz Behich",         Position.DEF),
        P("AUS", "Nathaniel Atkinson",  Position.DEF),
        P("AUS", "Kye Rowles",          Position.DEF),
        P("AUS", "Jackson Irvine",      Position.MID),
        P("AUS", "Ajdin Hrustic",       Position.MID),
        P("AUS", "Riley McGree",        Position.MID),
        P("AUS", "Keanu Baccus",        Position.MID),
        P("AUS", "Mathew Leckie",       Position.FWD),
        P("AUS", "Mitchell Duke",       Position.FWD),
        P("AUS", "Martin Boyle",        Position.FWD),
        P("AUS", "Craig Goodwin",       Position.FWD),

        // VEN
        P("VEN", "Wuilker Faríñez",     Position.GK),
        P("VEN", "Rafael Romo",         Position.GK),
        P("VEN", "Alexander González",  Position.DEF),
        P("VEN", "Nahuel Ferraresi",    Position.DEF),
        P("VEN", "Ronald Hernández",    Position.DEF),
        P("VEN", "Yordan Osorio",       Position.DEF),
        P("VEN", "Miguel Navarro",      Position.DEF),
        P("VEN", "Tomás Rincón",        Position.MID),
        P("VEN", "Yangel Herrera",      Position.MID),
        P("VEN", "Sergio Córdova",      Position.MID),
        P("VEN", "Rómulo Otero",        Position.MID),
        P("VEN", "Josef Martínez",      Position.FWD),
        P("VEN", "Salomón Rondón",      Position.FWD),
        P("VEN", "Adalberto Peñaranda", Position.FWD),
        P("VEN", "Jefferson Savarino",  Position.FWD),

        // ════════════════════════════════════════════════════════════════════
        // GROUP H
        // ════════════════════════════════════════════════════════════════════

        // NED
        P("NED", "Jasper Cillessen",    Position.GK),
        P("NED", "Bart Verbruggen",     Position.GK),
        P("NED", "Virgil van Dijk",     Position.DEF),
        P("NED", "Stefan de Vrij",      Position.DEF),
        P("NED", "Denzel Dumfries",     Position.DEF),
        P("NED", "Daley Blind",         Position.DEF),
        P("NED", "Nathan Aké",          Position.DEF),
        P("NED", "Frenkie de Jong",     Position.MID),
        P("NED", "Georginio Wijnaldum", Position.MID),
        P("NED", "Davy Klaassen",       Position.MID),
        P("NED", "Tijjani Reijnders",   Position.MID),
        P("NED", "Memphis Depay",       Position.FWD),
        P("NED", "Wout Weghorst",       Position.FWD),
        P("NED", "Cody Gakpo",          Position.FWD),
        P("NED", "Steven Bergwijn",     Position.FWD),
        P("NED", "Donyell Malen",       Position.FWD),

        // NOR
        P("NOR", "Ørjan Nyland",        Position.GK),
        P("NOR", "Rune Almenning Jarstein", Position.GK),
        P("NOR", "Leo Skiri Østigård",  Position.DEF),
        P("NOR", "Kristoffer Ajer",     Position.DEF),
        P("NOR", "Omar Elabdellaoui",   Position.DEF),
        P("NOR", "Birger Meling",       Position.DEF),
        P("NOR", "Andreas Hanche-Olsen", Position.DEF),
        P("NOR", "Sander Berge",        Position.MID),
        P("NOR", "Morten Thorsby",      Position.MID),
        P("NOR", "Martin Ødegaard",     Position.MID),
        P("NOR", "Fredrik Aursnes",     Position.MID),
        P("NOR", "Erling Haaland",      Position.FWD),
        P("NOR", "Alexander Sørloth",   Position.FWD),
        P("NOR", "Joshua King",         Position.FWD),
        P("NOR", "Mohamed Elyounoussi", Position.FWD),

        // CIV
        P("CIV", "Badra Ali Sangaré",   Position.GK),
        P("CIV", "Sylvain Gbohouo",     Position.GK),
        P("CIV", "Wilfried Kanon",      Position.DEF),
        P("CIV", "Serge Aurier",        Position.DEF),
        P("CIV", "Eric Bailly",         Position.DEF),
        P("CIV", "Ghislain Konan",      Position.DEF),
        P("CIV", "Odilon Kossounou",    Position.DEF),
        P("CIV", "Franck Kessié",       Position.MID),
        P("CIV", "Jean-Michaël Seri",   Position.MID),
        P("CIV", "Ibrahim Sangaré",     Position.MID),
        P("CIV", "Sébastien Haller",    Position.FWD),
        P("CIV", "Nicolas Pépé",        Position.FWD),
        P("CIV", "Jonathan Kodjia",     Position.FWD),
        P("CIV", "Wilfried Zaha",       Position.FWD),
        P("CIV", "Simon Adingra",       Position.FWD),

        // CHI
        P("CHI", "Claudio Bravo",       Position.GK),
        P("CHI", "Gabriel Arias",       Position.GK),
        P("CHI", "Gary Medel",          Position.DEF),
        P("CHI", "Guillermo Maripán",   Position.DEF),
        P("CHI", "Mauricio Isla",       Position.DEF),
        P("CHI", "Benjamín Kuscevic",   Position.DEF),
        P("CHI", "Paulo Díaz",          Position.DEF),
        P("CHI", "Erick Pulgar",        Position.MID),
        P("CHI", "Charles Aránguiz",    Position.MID),
        P("CHI", "Arturo Vidal",        Position.MID),
        P("CHI", "Alexis Sánchez",      Position.FWD),
        P("CHI", "Ben Brereton Díaz",   Position.FWD),
        P("CHI", "Iván Morales",        Position.FWD),
        P("CHI", "Eduardo Vargas",      Position.FWD),
        P("CHI", "Esteban Pavez",       Position.MID),

        // ════════════════════════════════════════════════════════════════════
        // GROUP I
        // ════════════════════════════════════════════════════════════════════

        // BEL
        P("BEL", "Thibaut Courtois",    Position.GK),
        P("BEL", "Simon Mignolet",      Position.GK),
        P("BEL", "Toby Alderweireld",   Position.DEF),
        P("BEL", "Jan Vertonghen",      Position.DEF),
        P("BEL", "Thomas Meunier",      Position.DEF),
        P("BEL", "Timothy Castagne",    Position.DEF),
        P("BEL", "Arthur Theate",       Position.DEF),
        P("BEL", "Axel Witsel",         Position.MID),
        P("BEL", "Kevin De Bruyne",     Position.MID),
        P("BEL", "Youri Tielemans",     Position.MID),
        P("BEL", "Amadou Onana",        Position.MID),
        P("BEL", "Romelu Lukaku",       Position.FWD),
        P("BEL", "Eden Hazard",         Position.FWD), // TODO: verify squad inclusion
        P("BEL", "Dries Mertens",       Position.FWD),
        P("BEL", "Lois Openda",         Position.FWD),
        P("BEL", "Leandro Trossard",    Position.FWD),

        // TUR
        P("TUR", "Uğurcan Çakır",       Position.GK),
        P("TUR", "Mert Günok",          Position.GK),
        P("TUR", "Merih Demiral",        Position.DEF),
        P("TUR", "Çağlar Söyüncü",      Position.DEF),
        P("TUR", "Zeki Çelik",          Position.DEF),
        P("TUR", "Ferdi Kadıoğlu",      Position.DEF),
        P("TUR", "Samet Akaydın",       Position.DEF),
        P("TUR", "Hakan Çalhanoğlu",    Position.MID),
        P("TUR", "Okay Yokuslu",        Position.MID),
        P("TUR", "Barış Alper Yılmaz",  Position.MID),
        P("TUR", "Kerem Aktürkoğlu",    Position.FWD),
        P("TUR", "Burak Yılmaz",        Position.FWD),
        P("TUR", "Cenk Tosun",          Position.FWD),
        P("TUR", "Arda Güler",          Position.MID),
        P("TUR", "Yusuf Yazıcı",        Position.FWD),

        // GHA
        P("GHA", "Lawrence Ati-Zigi",   Position.GK),
        P("GHA", "Richard Ofori",       Position.GK),
        P("GHA", "Daniel Amartey",      Position.DEF),
        P("GHA", "Alexander Djiku",     Position.DEF),
        P("GHA", "Tariq Lamptey",       Position.DEF),
        P("GHA", "Abdul Rahman Baba",   Position.DEF),
        P("GHA", "Jonathan Mensah",     Position.DEF),
        P("GHA", "Thomas Partey",       Position.MID),
        P("GHA", "Mubarak Wakaso",      Position.MID),
        P("GHA", "André Ayew",          Position.FWD),
        P("GHA", "Jordan Ayew",         Position.FWD),
        P("GHA", "Mohammed Salisu",     Position.DEF),
        P("GHA", "Kudus Mohammed",       Position.MID),
        P("GHA", "Antoine Semenyo",     Position.FWD),
        P("GHA", "Inaki Williams",      Position.FWD),

        // KSA
        P("KSA", "Mohammed Al-Owais",   Position.GK),
        P("KSA", "Fawaz Al-Qarni",      Position.GK),
        P("KSA", "Ali Al-Bulaihi",      Position.DEF),
        P("KSA", "Hassan Al-Tambakti",  Position.DEF),
        P("KSA", "Saud Abdulhamid",     Position.DEF),
        P("KSA", "Mohammed Al-Breik",   Position.DEF),
        P("KSA", "Abdulelah Al-Malki",  Position.MID),
        P("KSA", "Sami Al-Najei",       Position.MID),
        P("KSA", "Salem Al-Dawsari",    Position.FWD),
        P("KSA", "Mohammed Al-Shehri",  Position.FWD),
        P("KSA", "Firas Al-Buraikan",   Position.FWD),
        P("KSA", "Saleh Al-Shehri",     Position.FWD),
        P("KSA", "Fahad Al-Muwallad",   Position.FWD),
        P("KSA", "Ali Al-Hassan",       Position.MID), // TODO: verify
        P("KSA", "Mohammed Al-Najei",   Position.MID), // TODO: verify

        // ════════════════════════════════════════════════════════════════════
        // GROUP J
        // ════════════════════════════════════════════════════════════════════

        // KOR
        P("KOR", "Kim Seung-gyu",       Position.GK),
        P("KOR", "Jo Hyeon-woo",        Position.GK),
        P("KOR", "Kim Min-jae",         Position.DEF),
        P("KOR", "Jung Seung-hyun",     Position.DEF),
        P("KOR", "Kim Tae-hwan",        Position.DEF),
        P("KOR", "Kim Jin-su",          Position.DEF),
        P("KOR", "Lee Ki-je",           Position.DEF), // TODO: verify
        P("KOR", "Hwang In-beom",       Position.MID),
        P("KOR", "Jung Woo-young",      Position.MID),
        P("KOR", "Lee Jae-sung",        Position.MID),
        P("KOR", "Son Heung-min",       Position.FWD),
        P("KOR", "Hwang Hee-chan",      Position.FWD),
        P("KOR", "Cho Gue-sung",        Position.FWD),
        P("KOR", "Lee Kang-in",         Position.MID),
        P("KOR", "Hwang Ui-jo",         Position.FWD),

        // AUT
        P("AUT", "Daniel Bachmann",     Position.GK),
        P("AUT", "Patrick Pentz",       Position.GK),
        P("AUT", "David Alaba",         Position.DEF),
        P("AUT", "Maximilian Wöber",    Position.DEF),
        P("AUT", "Stefan Lainer",       Position.DEF),
        P("AUT", "Philipp Mwene",       Position.DEF),
        P("AUT", "Kevin Danso",         Position.DEF),
        P("AUT", "Florian Grillitsch",  Position.MID),
        P("AUT", "Konrad Laimer",       Position.MID),
        P("AUT", "Nicolas Seiwald",     Position.MID),
        P("AUT", "Marcel Sabitzer",     Position.MID),
        P("AUT", "Christoph Baumgartner", Position.FWD),
        P("AUT", "Marko Arnautovic",    Position.FWD),
        P("AUT", "Michael Gregoritsch", Position.FWD),
        P("AUT", "Patrick Wimmer",      Position.FWD),

        // QAT
        P("QAT", "Saad Al Sheeb",       Position.GK),
        P("QAT", "Meshaal Barsham",     Position.GK),
        P("QAT", "Bassam Al-Rawi",      Position.DEF),
        P("QAT", "Pedro Miguel",        Position.DEF),
        P("QAT", "Homam Ahmed",         Position.DEF),
        P("QAT", "Boualem Khoukhi",     Position.DEF),
        P("QAT", "Abdelkarim Hassan",   Position.DEF),
        P("QAT", "Karim Boudiaf",       Position.MID),
        P("QAT", "Abdulaziz Hatem",     Position.MID),
        P("QAT", "Assim Madibo",        Position.MID),
        P("QAT", "Akram Afif",          Position.FWD),
        P("QAT", "Almoez Ali",          Position.FWD),
        P("QAT", "Hassan Al-Haydos",    Position.FWD),
        P("QAT", "Mohammed Muntari",    Position.FWD),
        P("QAT", "Ismaeel Mohammad",    Position.MID), // TODO: verify

        // ALG
        P("ALG", "Raïs M'Bolhi",        Position.GK),
        P("ALG", "Alexandre Oukidja",   Position.GK),
        P("ALG", "Djamel Benlamri",     Position.DEF),
        P("ALG", "Ramy Bensebaïni",     Position.DEF),
        P("ALG", "Aïssa Mandi",         Position.DEF),
        P("ALG", "Mehdi Zeffane",       Position.DEF),
        P("ALG", "Youcef Atal",         Position.DEF),
        P("ALG", "Ismael Bennacer",     Position.MID),
        P("ALG", "Houssem Aouar",       Position.MID),
        P("ALG", "Sofiane Feghouli",    Position.MID),
        P("ALG", "Hichem Boudaoui",     Position.MID),
        P("ALG", "Islam Slimani",       Position.FWD),
        P("ALG", "Yacine Brahimi",      Position.FWD),
        P("ALG", "Riyad Mahrez",        Position.FWD),
        P("ALG", "Baghdad Bounedjah",   Position.FWD),

        // ════════════════════════════════════════════════════════════════════
        // GROUP K
        // ════════════════════════════════════════════════════════════════════

        // POL
        P("POL", "Wojciech Szczęsny",   Position.GK),
        P("POL", "Łukasz Fabiański",    Position.GK),
        P("POL", "Kamil Glik",          Position.DEF),
        P("POL", "Jan Bednarek",        Position.DEF),
        P("POL", "Bartosz Bereszyński", Position.DEF),
        P("POL", "Maciej Rybus",        Position.DEF),
        P("POL", "Matty Cash",          Position.DEF),
        P("POL", "Grzegorz Krychowiak", Position.MID),
        P("POL", "Mateusz Klich",       Position.MID),
        P("POL", "Piotr Zieliński",     Position.MID),
        P("POL", "Przemysław Frankowski", Position.MID),
        P("POL", "Robert Lewandowski",  Position.FWD),
        P("POL", "Arkadiusz Milik",     Position.FWD),
        P("POL", "Krzysztof Piątek",    Position.FWD),
        P("POL", "Karol Świderski",     Position.FWD),

        // SUI
        P("SUI", "Yann Sommer",         Position.GK),
        P("SUI", "Jonas Omlin",         Position.GK),
        P("SUI", "Manuel Akanji",       Position.DEF),
        P("SUI", "Nico Elvedi",         Position.DEF),
        P("SUI", "Kevin Mbabu",         Position.DEF),
        P("SUI", "Ricardo Rodríguez",   Position.DEF),
        P("SUI", "Fabian Schär",        Position.DEF),
        P("SUI", "Granit Xhaka",        Position.MID),
        P("SUI", "Denis Zakaria",       Position.MID),
        P("SUI", "Ruben Vargas",        Position.MID),
        P("SUI", "Remo Freuler",        Position.MID),
        P("SUI", "Haris Seferović",     Position.FWD),
        P("SUI", "Breel Embolo",        Position.FWD),
        P("SUI", "Xherdan Shaqiri",     Position.FWD),
        P("SUI", "Noah Okafor",         Position.FWD),
        P("SUI", "Zeki Amdouni",        Position.FWD),

        // RSA
        P("RSA", "Ronwen Williams",     Position.GK),
        P("RSA", "Ricardo Goss",        Position.GK),
        P("RSA", "Rushine de Reuck",    Position.DEF),
        P("RSA", "Yagan Sasman",        Position.DEF), // TODO: verify
        P("RSA", "Sifiso Hlanti",       Position.DEF),
        P("RSA", "Reeve Frosler",       Position.DEF),
        P("RSA", "Bongani Zungu",       Position.MID),
        P("RSA", "Njabulo Blom",        Position.MID),
        P("RSA", "Keagan Dolly",        Position.FWD),
        P("RSA", "Percy Tau",           Position.FWD),
        P("RSA", "Lyle Foster",         Position.FWD),
        P("RSA", "Themba Zwane",        Position.FWD),
        P("RSA", "Teboho Mokoena",       Position.MID),
        P("RSA", "Evidence Makgopa",    Position.FWD),
        P("RSA", "Thabiso Kutumela",    Position.FWD),

        // EGY
        P("EGY", "Mohamed El-Shenawy",  Position.GK),
        P("EGY", "Ahmed El-Shenawy",    Position.GK),
        P("EGY", "Ahmed Hegazy",        Position.DEF),
        P("EGY", "Omar Gaber",          Position.DEF),
        P("EGY", "Ahmed Fatouh",        Position.DEF),
        P("EGY", "Akram Tawfik",        Position.DEF),
        P("EGY", "Mohamed Abdelmonem",  Position.DEF),
        P("EGY", "Tarek Hamed",         Position.MID),
        P("EGY", "Amr El-Sulaya",       Position.MID),
        P("EGY", "Mohamed Elneny",      Position.MID),
        P("EGY", "Omar Marmoush",       Position.FWD),
        P("EGY", "Mohamed Salah",       Position.FWD),
        P("EGY", "Mostafa Mohamed",     Position.FWD),
        P("EGY", "Mahmoud Trezeguet",   Position.FWD),
        P("EGY", "Ramadan Sobhi",       Position.FWD),

        // ════════════════════════════════════════════════════════════════════
        // GROUP L
        // ════════════════════════════════════════════════════════════════════

        // PER
        P("PER", "Pedro Gallese",       Position.GK),
        P("PER", "José Carvallo",       Position.GK),
        P("PER", "Luis Abram",          Position.DEF),
        P("PER", "Alexander Callens",   Position.DEF),
        P("PER", "Miguel Trauco",       Position.DEF),
        P("PER", "Aldo Corzo",          Position.DEF),
        P("PER", "Carlos Zambrano",     Position.DEF),
        P("PER", "Renato Tapia",        Position.MID),
        P("PER", "Yoshimar Yotún",      Position.MID),
        P("PER", "Andy Polo",           Position.MID), // TODO: verify
        P("PER", "Sergio Peña",         Position.MID),
        P("PER", "André Carrillo",      Position.FWD),
        P("PER", "Gianluca Lapadula",   Position.FWD),
        P("PER", "Edison Flores",       Position.FWD),
        P("PER", "Christian Cueva",     Position.MID),
        P("PER", "Paolo Guerrero",      Position.FWD), // TODO: verify squad inclusion at age

        // UZB
        P("UZB", "Utkir Yusupov",       Position.GK),
        P("UZB", "Sanjar Kuvvatov",     Position.GK),
        P("UZB", "Husayn Norchaev",     Position.DEF), // TODO: verify
        P("UZB", "Dostonbek Khamdamov", Position.DEF), // TODO: verify
        P("UZB", "Anzur Ismailov",      Position.DEF), // TODO: verify
        P("UZB", "Abbosbek Fayzullaev", Position.MID),
        P("UZB", "Jaloliddin Masharipov", Position.MID),
        P("UZB", "Eldor Shomurodov",    Position.FWD),
        P("UZB", "Otabek Shukurov",     Position.MID), // TODO: verify
        P("UZB", "Jasur Yakhshiboev",   Position.FWD), // TODO: verify
        P("UZB", "Dostonbek Tursunov",  Position.FWD), // TODO: verify
        P("UZB", "Ikrom Alibaev",       Position.DEF), // TODO: verify
        P("UZB", "Umid Ashrapov",       Position.MID), // TODO: verify
        P("UZB", "Rustam Ashurmatov",   Position.DEF), // TODO: verify
        P("UZB", "Bobir Abdikholiqov",  Position.FWD), // TODO: verify

        // IRQ
        P("IRQ", "Jalal Hassan",        Position.GK),
        P("IRQ", "Mohammed Hameed",     Position.GK),
        P("IRQ", "Ahmed Ibrahim",       Position.DEF), // TODO: verify
        P("IRQ", "Ali Adnan",           Position.DEF),
        P("IRQ", "Saad Abdul Ameer",    Position.DEF), // TODO: verify
        P("IRQ", "Saman Quasim",        Position.DEF),
        P("IRQ", "Alaa Abbas",          Position.MID), // TODO: verify
        P("IRQ", "Amjad Attwan",        Position.MID),
        P("IRQ", "Bashar Resan",        Position.MID),
        P("IRQ", "Aymen Hussein",       Position.FWD),
        P("IRQ", "Mohanad Ali",         Position.FWD),
        P("IRQ", "Ahmed Yasin",         Position.MID),
        P("IRQ", "Emad Mohammed",       Position.FWD),
        P("IRQ", "Hammadi Ahmed",       Position.FWD),
        P("IRQ", "Ali Jasim",           Position.MID), // TODO: verify

        // PAR
        P("PAR", "Antony Silva",        Position.GK),
        P("PAR", "Alfredo Aguilar",     Position.GK),
        P("PAR", "Junior Alonso",       Position.DEF),
        P("PAR", "Gustavo Gómez",       Position.DEF),
        P("PAR", "Iván Piris",          Position.DEF),
        P("PAR", "Santiago Arzamendia", Position.DEF),
        P("PAR", "Fabián Balbuena",     Position.DEF),
        P("PAR", "Richard Sánchez",     Position.MID),
        P("PAR", "Miguel Almirón",      Position.MID),
        P("PAR", "Matías Rojas",        Position.MID),
        P("PAR", "Ángel Romero",        Position.FWD),
        P("PAR", "Óscar Romero",        Position.FWD),
        P("PAR", "Alejandro Romero",    Position.FWD),
        P("PAR", "Antonio Sanabria",    Position.FWD),
        P("PAR", "Robert Morales",      Position.FWD), // TODO: verify
    ];
}
