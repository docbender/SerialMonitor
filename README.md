# SerialMonitor
Program is simple piece of software designed for serial port data monitoring together with the possibility to automatically reply to sender. 

[![Version](https://img.shields.io/github/v/release/docbender/SerialMonitor?include_prereleases)](https://github.com/docbender/SerialMonitor/releases)
[![Download](https://img.shields.io/github/downloads/docbender/SerialMonitor/total.svg)](https://github.com/docbender/SerialMonitor/releases)
[![Issues](https://img.shields.io/github/issues/docbender/SerialMonitor)](https://github.com/docbender/SerialMonitor/issues)

Time between received data messages is displayed (in miliseconds). In addition to the printing on the screen it allows write everything (or just communication) to a specified file.

Program supported .NET6. Program is running under Windows or Linux. Program isn't serial port sniffer so can't monitor port that is already open by another program.

![](https://github.com/docbender/SerialMonitor/blob/master/img/SM3.png)

Program has also feature that allows answer to sender for specific message. This could be used for simple simulation of some device. To use this feature it is necessary to prepare file with pairs ask/answer. In file first(odd) line represent ask and second(even) line answer. File can contain several pairs ask/answer which can be separated by empty lines (not necessary). File example:

    0x01 0xD0 0x00 0xD1  - ask
    0x01 0xE2 0x00 0xE3  - answer
    
    0x75 0x55 0x55
    0x45 0x44 0x44
    
    0x11 0x54 0x55
    0x25 0x26 0x56

Data in file can be written in hex format (0x leading) or ASCII format. Selected format must be used through whole file.

##Usage
Program as commandline program support some parameters. First one (most important) PortName that represent port to open (ex. COM3 or /dev/ttyS1). When program is started without parameters port COM1 with default parameters is used.

**serialmonitor PortName [switch parameter]**

Switches:
* -baudrate {baud rate}: set port baud rate. Default 9600kbps.
* -parity {used parity}: set used port parity. Default none. Available parameters odd, even, mark and space.
* -databits {used databits}: set data bits count. Default 8 data bits.
* -stopbits {used stopbits}: set stop bits count. Default 1 stop bit. Available parameters 0, 1, 1.5 and 2.
* -repeatfile {file name}: enable repeat mode with protocol specified in file.
* -logfile {file name}: set file name for log into that file.
* -logincomingonly: log into file would be only incoming data.
* -showascii: communication would be show in ASCII format (otherwise HEX is used).
* -notime: time information about incoming data would not be printed.
* -gaptolerance {time gap in ms}: messages received within specified time gap will be printed together.
* -nogui: start program in normal console mode (scrolling list). Not with text GUI.

Example:

    serialmonitor COM1
    serialmonitor /dev/ttyS1 -baudrate 57600
    serialmonitor COM1 -baudrate 57600 -parity odd -databits 7 -stopbits 1.5
    serialmonitor COM83 -baudrate 19200 -repeatfile protocol.txt

In program commands can be typed:
* exit: program exit
* send {data to send}: send specified data (in HEX format if data start with 0x otherwise ASCII is send)