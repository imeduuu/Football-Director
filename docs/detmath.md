# Matemática determinista

`BT.Core.Sim.DetMath` contiene las operaciones matemáticas usadas por la simulación. En `BT.Core` el analizador prohíbe el tipo `System.Math`; las aproximaciones evalúan operaciones binarias `double` en el orden escrito y no dependen de funciones matemáticas del runtime.

| Método | Dominio admitido | Error máximo documentado |
| --- | --- | --- |
| `Ln(x)` | finito, `x > 0` | absoluto `2e-14` |
| `Exp(x)` | `-700 <= x <= 700`, finito | relativo `2e-14` |
| `Sqrt(x)` | finito, `x >= 0` | relativo `2e-15` para `x > 0` |
| `Sin(x)`, `Cos(x)` | `|x| <= 64`, finito, radianes | absoluto `3e-14` |
| `Pow(x, y)` | `x > 0`, ambos finitos, y `y*Ln(x)` dentro de `[-700, 700]` | relativo `5e-14` |

Fuera del dominio se lanza `ArgumentOutOfRangeException`. `Sqrt(0)` devuelve cero y `Pow(x, 0)` y `Pow(1, y)` devuelven uno. `Pow` admite bases positivas para una definición real general; las potencias de bases negativas no forman parte de esta API.

`Ln` normaliza la mantisa y usa la serie de `2*atanh((m-1)/(m+1))` hasta el denominador 25. `Exp` reduce por `ln(2)` y evalúa Taylor hasta grado 18. `Sqrt` normaliza entradas subnormales por `2^54`, usa una semilla obtenida de los bits del exponente y siete pasos de Newton, y escala el resultado por `2^-27`. `Sin` y `Cos` reducen por cuadrantes con `pi/2` dividido en parte alta y residual, y evalúan Horner hasta grados 17 y 18. `Pow` compone `Exp(y*Ln(x))`.

Los errores indicados son cotas conservadoras para binary64, incluyendo el truncamiento de las series y el redondeo de sus operaciones, dentro de los dominios declarados. Para reproducibilidad se mantiene el orden de evaluación escrito y no se debe compilar `BT.Core` con contracción de operaciones (FMA) o `fast-math` habilitadas.
