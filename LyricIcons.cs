using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.RegularExpressions;

static class LyricIcons
{
    static readonly Dictionary<string,string> Words = MakeDictionary();
    static readonly Regex RingSound = new Regex(@"^\W*(r+i+n+g?)(?:[\s\p{Pd},]*(r+i+n+g?))?\W*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static int CountRings(string text)
    {
        if (String.IsNullOrEmpty(text) || text.IndexOf("r",StringComparison.OrdinalIgnoreCase) < 0) return 0;
        var match = RingSound.Match(text);
        return !match.Success ? 0 : match.Groups[2].Success ? 2 : 1;
    }

    static Dictionary<string,string> MakeDictionary()
    {
        var map = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        Group(map,"eye","show shows see seeing seen look looking eyes eye watch watching mira mirar miras mirada miradas veo ver ojos ojo");
        Group(map,"hands","hand hands clap claps clapping palms mano manos palma palmas aplaude aplausos");
        Group(map,"heart","heart hearts heartbeat love loved loving kiss kisses corazón corazones amor amarte amores beso besos querer");
        Group(map,"star","star stars starlight sparkle sparkles shine shining estrella estrellas brilla brillan brillar destello");
        Group(map,"moon","moon moonlight lunar midnight night noche noches luna medianoche");
        Group(map,"sun","sun sunlight sunshine sunrise sunny daylight sol soleado amanecer");
        Group(map,"flame","fire fires flame flames burn burns burning fuego arde arder llamas incendio");
        Group(map,"drop","rain raindrop rainfalls tears teardrops tear crying cry lluvia gota gotas lágrimas lágrima llorar llora");
        Group(map,"wave","ocean oceans sea seas waves wave tide tides mar olas ola marea océano");
        Group(map,"note","music melody melodies sing singing sings song songs rhythm beat bass dance dancing música ritmo canción canciones cantar cantas baila bailar");
        Group(map,"world","world worlds earth planet mundo mundos tierra planeta");
        Group(map,"bolt","lightning thunder electric electricity rayo rayos relámpago trueno electricidad");
        Group(map,"clock","clock clocks time hora horas reloj tiempo");
        Group(map,"smile","smile smiles smiling grin laugh laughing sonrisa sonríe sonrisas risas reír");
        Group(map,"flower","flower flowers rose roses flor flores rosa rosas");
        Group(map,"person","people person boy girl man woman someone gente persona chico chica alguien");
        Group(map,"eye","saw gaze staring stare glance miro miré mírame mirame vi viste viendo verme verte");
        Group(map,"hands","hold holding held touch touching embrace abrázame abrazame abrazo abrazos abrazarte acariciar toca tócame");
        Group(map,"heart","loves lover lovers darling feel feels feeling feelings cariño corazon enamorado enamorada enamoré amo amas amaba amando amarnos quiéreme quererte quiero sientes siento sentir sentimientos");
        Group(map,"star","wish wishes wishing sparkling bright brillarás brillo brillando deseo deseos desear");
        Group(map,"moon","tonight nighttime dusk anoche anochecer trasnochar oscuridad oscura oscuro");
        Group(map,"sun","day days morning mornings dawn mañana mañanas madrugada día días soleada amaneció");
        Group(map,"flame","burned burnt heat hot caliente calor quemar quema queman quemando cenizas");
        Group(map,"drop","cried weep weeping sad sadness raining rainy lloro lloras llorando lloré triste tristeza llover llovía lagrimas");
        Group(map,"wave","water waters shore beach surf river agua playa playas costa río rios ríos mareas");
        Group(map,"note","danced dancer dances bailarás bailando baile canta canto cantando melodía melodias melodías musica cancion");
        Group(map,"world","everywhere universe universo continentes");
        Group(map,"clock","hours minutes seconds yesterday tomorrow forever always forevermore ayer siempre nunca jamás segundos minutos instante eternidad");
        Group(map,"smile","happy happiness laughed laughter glad feliz felicidad sonreír sonreir sonrío ríe ríes riendo");
        Group(map,"flower","petal petals blossom blooming garden gardens pétalo pétalos petalos jardín jardines florecer");
        Group(map,"person","somebody nobody baby babe friend friends amigo amiga amigos amigas bebé bebe nena nene nadie");
        Group(map,"💔","heartbreak heartbroken broken breaks shattered desamor roto rota rompo rompió rompiste romper romperme lastimas duele dolor");
        Group(map,"💋","lips lipstick besarte besar bésame besame labios boca boquita");
        Group(map,"💭","think thinks thinking thought thoughts wonder wondering mind imagino imaginar pienso piensas pensando pensé mente ideas");
        Group(map,"💤","sleep sleeps sleepy sleeping slept dream dreams dreamer dreaming dreamin sueño sueños soñar soñé soñando duerme duermo dormir");
        Group(map,"💬","say says saying said speak speaking tell telling told talk talking words digo dices dime decir decía dijiste habla háblame hablando palabras");
        Group(map,"📞","phone phones call calls calling called telephone teléfono telefono celular llamada llámame llamame llamarte llamando");
        Group(map,"📞","ringtone ringtones móvil movil timbra timbrando telefonito");
        Group(map,"📞","brrring rrring buzz buzzing vibrando");
        Group(map,"🔔","ding dong dingdong campana campanas campanita timbre cascabel repica");
        Group(map,"clock","tic tac tictac tick tock ticktock ticking");
        Group(map,"drop","drip dripping splash splashing gotea goteo chapoteo ploc");
        Group(map,"hands","aplaudir aplaudiendo palmada palmadas aplauso");
        Group(map,"🚗","vroom vrumm brum derrapa acelera acelerando motor motores");
        Group(map,"🚪","knock knocking toc toctoc portazo");
        Group(map,"💥","boom kaboom bang pum bum explosión estalla retumba estallido");
        Group(map,"🐱","gato gata gatos gatito gatita miau maúlla ronronea cat cats kitten kitty meow purr");
        Group(map,"🐶","perro perra perrito cachorro guau ladra dog dogs puppy woof bark barking");
        Group(map,"🕊","pío gorjeo trino chirp chirping tweet");
        Group(map,"✉","message messages letter letters mail text texting carta cartas mensaje mensajes escribo escríbeme escribeme escribirte");
        Group(map,"🧠","remember remembering remembered memory memories forget forgotten recuerdo recuerdos recuerda recuerdas acuérdate acordarme olvido olvidar olvidarte");
        Group(map,"👂","hear heard hearing listen listens listening escucha escucho escúchame escuchar oigo oír oyendo oído");
        Group(map,"🎤","voice voices chorus microphone mic vocals singer voz voces coro micrófono microfono cantante");
        Group(map,"🎧","headphones earphones stereo radio radios auriculares audífonos audifonos cascos estéreo");
        Group(map,"🎸","guitar guitars guitarist strings guitarra guitarras guitarrista cuerdas");
        Group(map,"🥁","drum drums drummer drumming tambor tambores batería bateria percusión redoble");
        Group(map,"🎹","piano keyboards pianist teclado teclas pianista pianos");
        Group(map,"🏠","home homes house houses hometown hogar hogares casa casas casita barrio");
        Group(map,"🚪","door doors doorway entrance exit puerta puertas entrada salida umbral");
        Group(map,"🔑","key keys unlock unlocked llave llaves abrir abre ábreme abrirte");
        Group(map,"🔒","lock locks locked secret secrets candado candados cerrado cerrada secreto secretos guardar");
        Group(map,"👣","walk walks walking walked step steps go going gone leave leaving feet barefoot paso pasos caminar caminando camino ando andar voy vete descalzo huellas");
        Group(map,"🏃","run runs ran running chase chasing corre corro correr corriendo huir escapando persigue");
        Group(map,"🛣","road roads highway highways street streets avenue crossroads calle calles carretera carreteras avenida sendero rumbo");
        Group(map,"🚗","car cars ride riding drive driving drove taxi coche coches carro carros auto autos manejar conduciendo volante");
        Group(map,"🚂","train trains railway railroad station tren trenes estación estacion andén rieles");
        Group(map,"✈","plane planes airplane flight flights fly flies flying flew avión avion aviones vuelo vuelos volar volando vuela vuelas vuelan");
        Group(map,"🧳","suitcase baggage luggage travel traveling journey trip maleta maletas equipaje viaje viajes viajar viajando");
        Group(map,"⚓","anchor anchors harbor harbour port sailor ancla anclas puerto marinero");
        Group(map,"⛵","boat boats ship ships sailing sail barco barcos velero navegar navegando");
        Group(map,"☁","cloud clouds cloudy sky skies heaven heavens nube nubes nublado cielo cielos");
        Group(map,"🌬","wind winds windy breeze breath breathe breathing viento vientos brisa aire aliento respirar respiro");
        Group(map,"❄","snow snowing snowy ice icy frost frozen cold nieve hielo helado helada frío fria fría congelado");
        Group(map,"🌈","rainbow rainbows colors colours colorful arcoíris arcoiris colores multicolor");
        Group(map,"⛰","mountain mountains hill hills climb climbing montaña montañas colina cumbre cima");
        Group(map,"🌲","tree trees forest woods árbol arbol árboles arboles bosque bosques pino");
        Group(map,"🍃","leaf leaves autumn otoño hoja hojas follaje");
        Group(map,"🦋","butterfly butterflies mariposa mariposas");
        Group(map,"🕊","bird birds dove doves wings wing freedom free pájaro pájaros ave aves paloma palomas alas libre libertad");
        Group(map,"🕯","candle candles candlelight luz luces vela velas llama iluminado light lights glow glowing");
        Group(map,"💎","diamond diamonds jewel jewels pearl treasure diamante diamantes joya joyas perla tesoro");
        Group(map,"👑","king kings queen queens crown royalty rey reyes reina reinas corona trono");
        Group(map,"💍","ring rings wedding marry married anillo anillos boda bodas casarme casarnos");
        Group(map,"🎭","mask masks pretend pretending disguise lies mentira mentiras mentir fingir fingiendo máscara máscaras disfraz");
        Group(map,"📷","photo photos photograph photographs picture pictures camera foto fotos fotografía fotografías cámara retrato");
        Group(map,"🪞","mirror mirrors reflection reflections espejo espejos reflejo reflejos");
        Group(map,"📖","book books story stories page pages chapter libro libros historia historias página páginas cuento capítulo");
        Group(map,"⏳","wait waits waited waiting patience espera espero esperando espérame esperarte paciencia");
        Group(map,"🧲","pull pulling magnet magnets magnetic attract attraction imán imanes magnetismo atracción atrae");
        Group(map,"🎲","dice gamble luck lucky chance chances dado dados suerte azar fortuna");
        Group(map,"🎉","party partying celebrate celebrating fiesta fiestas festejar celebrar celebración");
        Group(map,"🪩","disco nightclub dancefloor discoteca discotecas pista antro antros");
        Group(map,"🍷","wine drink drinks drinking drunk whiskey whisky tequila vino copa copas trago tragos borracho borracha");
        Group(map,"💵","money cash dollar dollars dinero billete billetes dólares dolares pesos");
        Group(map,"🧊","cube cubes iceberg icebergs cubito cubitos témpano");
        Group(map,"🧭","lost direction north south compass perdido perdida perdidos norte sur brújula brujula destino");
        Group(map,"🪐","space galaxy galaxies cosmos orbit saturn espacio galaxia galaxias órbita saturno");
        Group(map,"👻","ghost ghosts haunted haunting ghosting fantasma fantasmas embrujado");
        Group(map,"😈","devil devils demon demons devilish diablo diabla demonio demonios infierno");
        Group(map,"😇","angel angels halo angelic ángel ángeles angelito aureola paraíso");
        return map;
    }

    static void Group(Dictionary<string,string> map,string name,string words)
    {
        foreach (var word in words.Split(' ')) map.Add(word,name);
    }

    internal static string Match(string text)
    {
        if (String.IsNullOrWhiteSpace(text)) return null;
        int start=0, end=text.Length;
        while (start<end && !Char.IsLetterOrDigit(text[start])) start++;
        while (end>start && !Char.IsLetterOrDigit(text[end-1])) end--;
        string icon;
        return Words.TryGetValue(text.Substring(start,end-start),out icon) ? icon : null;
    }

    // Draw as part of the word outline so spacing, color and lens distortion stay together.
    public static void Append(GraphicsPath glyph,string word)
    {
        string name=Match(word);
        if (name==null) return;
        using (var icon=new GraphicsPath())
        {
            Draw(icon,name);
            WordMotion.Append(glyph,icon,name,-1,false);
        }
    }

    static void Stroke(GraphicsPath p,params float[] xy)
    {
        var points = new PointF[xy.Length/2];
        for(int i=0;i<points.Length;i++) points[i]=new PointF(xy[i*2],xy[i*2+1]);
        p.StartFigure();
        p.AddLines(points);
    }

    internal static void Draw(GraphicsPath p,string name)
    {
        switch(name)
        {
            case "eye":
                p.AddBezier(1,12,7,2,17,2,23,12);
                p.AddBezier(23,12,17,22,7,22,1,12);
                p.CloseFigure(); p.AddEllipse(9,8,6,8);
                break;
            case "hands":
                // A waving palm in profile, with two motion marks beside the fingers.
                p.AddBezier(7,14,4,10,2,11,3,13);
                p.AddLine(3,13,7,19); p.AddBezier(7,19,12,26,18,21,19,17);
                Stroke(p,19,17,21,9); p.AddBezier(21,9,22,6,19,6,18,9);
                p.AddLine(18,9,16,13); p.AddLine(16,13,19,4);
                p.AddBezier(19,4,19,2,17,2,16,4); p.AddLine(16,4,13,12);
                p.AddLine(13,12,15,3); p.AddBezier(15,3,15,1,13,1,12,3); p.AddLine(12,3,10,11);
                p.AddLine(10,11,11,5); p.AddBezier(11,5,11,3,9,3,8.5f,5); p.AddLine(8.5f,5,7,14);
                Stroke(p,3,7,1,5); Stroke(p,5,4,4,1);
                break;
            case "heart":
                p.AddBezier(12,21,10,19,1,13,2,7); p.AddBezier(2,7,3,1,10,1,12,7);
                p.AddBezier(12,7,15,1,22,2,22,8); p.AddBezier(22,8,22,13,15,19,12,21); p.CloseFigure();
                break;
            case "star":
                Stroke(p,12,1,15,8.5f,23,9,17,14.5f,19,22,12,18,5,22,7,14.5f,1,9,9,8.5f,12,1); p.CloseFigure();
                break;
            case "moon":
                p.AddBezier(17,2,5,-1,0,11,5,18); p.AddBezier(5,18,9,24,18,24,22,16);
                p.AddBezier(22,16,12,19,9,8,17,2); p.CloseFigure();
                break;
            case "sun":
                p.AddEllipse(6.5f,6.5f,11,11);
                for(int i=0;i<8;i++) { double a=i*Math.PI/4; float x=(float)Math.Cos(a),y=(float)Math.Sin(a); Stroke(p,12+x*8,12+y*8,12+x*11,12+y*11); }
                break;
            case "flame":
                p.AddBezier(12,1,14,8,8,8,9,13); p.AddBezier(9,13,6,12,5,10,5,8);
                p.AddBezier(5,8,-1,19,7,24,13,22); p.AddBezier(13,22,23,21,23,10,18,6);
                p.AddBezier(18,6,18,10,16,12,16,12); p.AddBezier(16,12,17,7,15,3,12,1); p.CloseFigure();
                break;
            case "drop":
                p.AddBezier(12,1,10,6,4,11,4,15); p.AddBezier(4,15,4,25,20,25,20,15);
                p.AddBezier(20,15,20,11,14,4,12,1); p.CloseFigure(); p.StartFigure(); p.AddBezier(8,16,8,18,9,19,11,19);
                break;
            case "wave":
                for(int y=7;y<=19;y+=6) {p.StartFigure(); p.AddBezier(1,y,5,y-6,8,y-6,12,y); p.AddBezier(12,y,16,y+5,19,y+4,23,y-1);}
                break;
            case "note":
                Stroke(p,7,17,7,6,20,3,20,15); Stroke(p,7,9,20,6); p.AddEllipse(1,16,6,5); p.AddEllipse(14,14,6,5);
                break;
            case "world":
                p.AddEllipse(1,1,22,22); p.AddEllipse(7,1,10,22); Stroke(p,2,9,22,9); Stroke(p,2,15,22,15);
                break;
            case "bolt":
                Stroke(p,14,1,3,14,11,14,9,23,22,9,14,9,16,1); p.CloseFigure();
                break;
            case "clock":
                p.AddEllipse(1,1,22,22); Stroke(p,12,5,12,12,17,15);
                break;
            case "smile":
                p.AddEllipse(1,1,22,22); Stroke(p,8,8,8,10); Stroke(p,16,8,16,10);
                p.StartFigure();p.AddBezier(6,14,8,20,16,20,18,14);
                break;
            case "flower":
                for(int i=0;i<5;i++) {double a=(i*72-90)*Math.PI/180;p.AddEllipse((float)(8+6*Math.Cos(a)),(float)(8+6*Math.Sin(a)),8,8);}
                p.AddEllipse(9.3f,9.3f,5.4f,5.4f);
                break;
            case "person":
                p.AddEllipse(8,1,8,8); p.StartFigure(); p.AddBezier(3,22,3,9,21,9,21,22); p.CloseFigure();
                break;
            default:
                using (var family = new FontFamily("Segoe UI Emoji"))
                    p.AddString(name, family, (int)FontStyle.Regular, 24, PointF.Empty, StringFormat.GenericTypographic);
                return;
        }
        using (var pen=new Pen(Color.Black,1.65f) { LineJoin=LineJoin.Round,StartCap=LineCap.Round,EndCap=LineCap.Round })
            p.Widen(pen);
    }
}
